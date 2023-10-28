#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NCode.Identity;
using NCode.Jose.Extensions;
using NCode.Jose.SecretKeys;
using NIdentity.OpenId.DataContracts;
using NIdentity.OpenId.Logic;
using NIdentity.OpenId.Mediator;
using NIdentity.OpenId.Options;
using NIdentity.OpenId.Results;
using NIdentity.OpenId.Stores;
using NIdentity.OpenId.Tenants.Commands;

namespace NIdentity.OpenId.Tenants.Handlers;

internal class DefaultTenantHandler :
    ICommandResponseHandler<GetOpenIdTenantCommand, OpenIdTenant>,
    ICommandResponseHandler<GetTenantConfigurationCommand, TenantConfiguration>,
    ICommandResponseHandler<GetTenantSecretsCommand, ISecretKeyProvider>,
    ICommandResponseHandler<GetTenantBaseAddressCommand, UriDescriptor>,
    ICommandResponseHandler<GetTenantIssuerCommand, string>
{
    private Regex? DomainNameRegex { get; set; }
    private OpenIdHostOptions HostOptions { get; }
    private TemplateBinderFactory TemplateBinderFactory { get; }
    private ITenantStore TenantStore { get; }
    private ISecretSerializer SecretSerializer { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTenantHandler"/> class.
    /// </summary>
    public DefaultTenantHandler(
        IOptions<OpenIdHostOptions> hostOptionsAccessor,
        TemplateBinderFactory templateBinderFactory,
        ITenantStore tenantStore,
        ISecretSerializer secretSerializer)
    {
        HostOptions = hostOptionsAccessor.Value;
        TemplateBinderFactory = templateBinderFactory;
        TenantStore = tenantStore;
        SecretSerializer = secretSerializer;
    }

    /// <inheritdoc />
    public async ValueTask<OpenIdTenant> HandleAsync(GetOpenIdTenantCommand command, CancellationToken cancellationToken)
    {
        var (httpContext, tenantRoute, mediator, propertyBag) = command;

        var configuration = await mediator.SendAsync<GetTenantConfigurationCommand, TenantConfiguration>(
            new GetTenantConfigurationCommand(
                httpContext,
                tenantRoute,
                mediator,
                propertyBag),
            cancellationToken);

        var baseAddress = await mediator.SendAsync<GetTenantBaseAddressCommand, UriDescriptor>(
            new GetTenantBaseAddressCommand(
                httpContext,
                tenantRoute,
                configuration,
                mediator,
                propertyBag),
            cancellationToken);

        var issuer = await mediator.SendAsync<GetTenantIssuerCommand, string>(
            new GetTenantIssuerCommand(
                httpContext,
                baseAddress,
                configuration,
                mediator,
                propertyBag),
            cancellationToken);

        var secretKeyProvider = await mediator.SendAsync<GetTenantSecretsCommand, ISecretKeyProvider>(
            new GetTenantSecretsCommand(
                httpContext,
                configuration,
                mediator,
                propertyBag),
            cancellationToken);

        httpContext.Response.RegisterForDispose(secretKeyProvider);

        return new DefaultOpenIdTenant(
            configuration,
            secretKeyProvider,
            baseAddress,
            issuer);
    }

    #region GetTenantConfigurationCommand

    /// <inheritdoc />
    public async ValueTask<TenantConfiguration> HandleAsync(
        GetTenantConfigurationCommand command,
        CancellationToken cancellationToken)
    {
        return HostOptions.Tenant.Mode switch
        {
            TenantMode.StaticSingle => GetTenantFromOptions(),
            TenantMode.DynamicByHost => await GetTenantFromHostAsync(command, cancellationToken),
            TenantMode.DynamicByPath => await GetTenantFromPathAsync(command, cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported TenantMode: {HostOptions.Tenant.Mode}")
        };
    }

    private static InvalidOperationException MissingTenantOptionsException(TenantMode mode) =>
        new($"The TenantMode is '{mode}' but the corresponding options are missing.");

    private TenantConfiguration GetTenantFromOptions()
    {
        var options = HostOptions.Tenant.StaticSingle;
        if (options is null)
            throw MissingTenantOptionsException(TenantMode.StaticSingle);

        var configuration = options.TenantConfiguration;

        if (string.IsNullOrEmpty(configuration.TenantId))
            configuration.TenantId = StaticSingleOpenIdTenantOptions.DefaultTenantId;

        if (string.IsNullOrEmpty(configuration.DisplayName))
            configuration.DisplayName = StaticSingleOpenIdTenantOptions.DefaultDisplayName;

        return configuration;
    }

    private async ValueTask<TenantConfiguration> GetTenantFromHostAsync(
        GetTenantConfigurationCommand command,
        CancellationToken cancellationToken)
    {
        var options = HostOptions.Tenant.DynamicByHost;
        if (options is null)
            throw MissingTenantOptionsException(TenantMode.DynamicByHost);

        var regex = DomainNameRegex ??= new Regex(
            options.RegexPattern,
            RegexOptions.Compiled |
            RegexOptions.CultureInvariant |
            RegexOptions.Singleline);

        var host = command.HttpContext.Request.Host.Host;
        var match = regex.Match(host);
        var domainName = match.Success ? match.Value : host;

        var tenant = await TenantStore.TryGetByDomainNameAsync(domainName, cancellationToken);
        if (tenant is null)
            throw TypedResults.NotFound().AsException($"A tenant with domain '{domainName}' could not be found.");

        command.PropertyBag.Set(tenant);

        return tenant.Configuration;
    }

    private async ValueTask<TenantConfiguration> GetTenantFromPathAsync(
        GetTenantConfigurationCommand command,
        CancellationToken cancellationToken)
    {
        var options = HostOptions.Tenant.DynamicByPath;
        if (options == null)
            throw MissingTenantOptionsException(TenantMode.DynamicByPath);

        var httpContext = command.HttpContext;
        var tenantRoute = command.TenantRoute;

        if (tenantRoute is null)
            throw new InvalidOperationException("The TenantRoute is null.");

        if (tenantRoute.Parameters.Count == 0)
            throw new InvalidOperationException("The TenantRoute has no parameters.");

        var httpRequest = httpContext.Request;
        var routeValues = httpRequest.RouteValues;

        if (!routeValues.TryGetValue(options.TenantIdRouteParameterName, out var routeValue))
            throw new InvalidOperationException($"The value for route parameter '{options.TenantIdRouteParameterName}' could not be found.");

        if (routeValue is not string tenantId)
            throw new InvalidOperationException($"The value for route parameter '{options.TenantIdRouteParameterName}' is not a string.");

        if (string.IsNullOrEmpty(tenantId))
            throw new InvalidOperationException($"The value for route parameter '{options.TenantIdRouteParameterName}' is empty.");

        var tenant = await TenantStore.TryGetByTenantIdAsync(tenantId, cancellationToken);
        if (tenant is null)
            throw TypedResults.NotFound().AsException($"A tenant with identifier '{tenantId}' could not be found.");

        command.PropertyBag.Set(tenant);

        return tenant.Configuration;
    }

    #endregion

    #region GetTenantSecretsCommand

    /// <inheritdoc />
    public async ValueTask<ISecretKeyProvider> HandleAsync(
        GetTenantSecretsCommand command,
        CancellationToken cancellationToken)
    {
        if (command.PropertyBag.TryGet<Tenant>(out var tenant))
        {
            return DeserializeSecrets(tenant.Secrets);
        }

        if (HostOptions.Tenant.Mode == TenantMode.StaticSingle)
        {
            var serviceProvider = command.HttpContext.RequestServices;
            return serviceProvider.GetRequiredService<ISecretKeyProvider>();
        }

        var tenantId = command.Configuration.TenantId;
        tenant = await TenantStore.TryGetByTenantIdAsync(tenantId, cancellationToken);
        if (tenant is null)
            throw TypedResults.NotFound().AsException($"A tenant with identifier '{tenantId}' could not be found.");

        return DeserializeSecrets(tenant.Secrets);
    }

    private ISecretKeyProvider DeserializeSecrets(IEnumerable<Secret> secrets)
    {
        var secretKeys = SecretSerializer.DeserializeSecrets(secrets);
        try
        {
            // TODO: add support for a dynamic data source that re-fetches secrets from the store
            var dataSource = new StaticSecretKeyDataSource(secretKeys);
            var provider = SecretKeyProvider.Create(dataSource);
            return provider;
        }
        catch
        {
            secretKeys.DisposeAll(ignoreExceptions: true);
            throw;
        }
    }

    #endregion

    #region GetTenantBaseAddressCommand

    /// <inheritdoc />
    public ValueTask<UriDescriptor> HandleAsync(GetTenantBaseAddressCommand command, CancellationToken cancellationToken)
    {
        var httpContext = command.HttpContext;
        var tenantRoute = command.TenantRoute;

        var httpRequest = httpContext.Request;
        var basePath = httpRequest.PathBase;

        if (tenantRoute is not null)
        {
            var templateBinder = TemplateBinderFactory.Create(tenantRoute);
            var tenantRouteUrl = templateBinder.BindValues(httpRequest.RouteValues);
            if (!string.IsNullOrEmpty(tenantRouteUrl))
            {
                basePath.Add(tenantRouteUrl);
            }
        }

        var baseAddress = new UriDescriptor
        {
            Scheme = httpRequest.Scheme,
            Host = httpRequest.Host,
            Path = basePath
        };

        return ValueTask.FromResult(baseAddress);
    }

    #endregion

    #region GetTenantIssuerCommand

    /// <inheritdoc />
    public ValueTask<string> HandleAsync(GetTenantIssuerCommand command, CancellationToken cancellationToken)
    {
        var baseAddress = command.BaseAddress;
        var tenantConfiguration = command.Configuration;

        var issuer = tenantConfiguration.Issuer;
        if (string.IsNullOrEmpty(issuer))
            issuer = baseAddress.ToString();

        return ValueTask.FromResult(issuer);
    }

    #endregion
}
