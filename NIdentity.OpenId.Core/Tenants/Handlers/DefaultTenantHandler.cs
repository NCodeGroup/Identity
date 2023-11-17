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
using NIdentity.OpenId.Servers;
using NIdentity.OpenId.Settings;
using NIdentity.OpenId.Stores;
using NIdentity.OpenId.Tenants.Commands;

namespace NIdentity.OpenId.Tenants.Handlers;

/// <summary>
/// Provides a default implementation of the required services and handlers for <see cref="OpenIdTenant"/>.
/// </summary>
public class DefaultTenantHandler :
    ICommandResponseHandler<GetOpenIdTenantCommand, OpenIdTenant>,
    ICommandResponseHandler<GetTenantDescriptorCommand, TenantDescriptor>,
    ICommandResponseHandler<GetTenantSettingsCommand, ISettingCollection>,
    ICommandResponseHandler<GetTenantSecretsCommand, ISecretKeyProvider>,
    ICommandResponseHandler<GetTenantBaseAddressCommand, UriDescriptor>,
    ICommandResponseHandler<GetTenantIssuerCommand, string>
{
    private Regex? DomainNameRegex { get; set; }
    private OpenIdServerOptions ServerOptions { get; }
    private TemplateBinderFactory TemplateBinderFactory { get; }
    private ITenantStore TenantStore { get; }
    private ISecretSerializer SecretSerializer { get; }
    private IOpenIdServerSettingsProvider ServerSettingsProvider { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTenantHandler"/> class.
    /// </summary>
    public DefaultTenantHandler(
        IOptions<OpenIdServerOptions> serverOptionsAccessor,
        TemplateBinderFactory templateBinderFactory,
        ITenantStore tenantStore,
        ISecretSerializer secretSerializer,
        IOpenIdServerSettingsProvider serverSettingsProvider)
    {
        ServerOptions = serverOptionsAccessor.Value;
        TemplateBinderFactory = templateBinderFactory;
        TenantStore = tenantStore;
        SecretSerializer = secretSerializer;
        ServerSettingsProvider = serverSettingsProvider;
    }

    private async ValueTask<Tenant> GetTenantByIdAsync(string tenantId, CancellationToken cancellationToken)
    {
        var tenant = await TenantStore.TryGetByTenantIdAsync(tenantId, cancellationToken);
        if (tenant is null)
            throw TypedResults.NotFound().AsException($"A tenant with identifier '{tenantId}' could not be found.");
        return tenant;
    }

    private async ValueTask<Tenant> GetTenantByDomainAsync(string domainName, CancellationToken cancellationToken)
    {
        var tenant = await TenantStore.TryGetByDomainNameAsync(domainName, cancellationToken);
        if (tenant is null)
            throw TypedResults.NotFound().AsException($"A tenant with domain '{domainName}' could not be found.");
        return tenant;
    }

    /// <inheritdoc />
    public async ValueTask<OpenIdTenant> HandleAsync(GetOpenIdTenantCommand command, CancellationToken cancellationToken)
    {
        var (httpContext, tenantRoute, mediator, propertyBag) = command;

        var tenantDescriptor = await mediator.SendAsync<GetTenantDescriptorCommand, TenantDescriptor>(
            new GetTenantDescriptorCommand(
                httpContext,
                tenantRoute,
                mediator,
                propertyBag),
            cancellationToken);

        var tenantSettings = await mediator.SendAsync<GetTenantSettingsCommand, ISettingCollection>(
            new GetTenantSettingsCommand(
                httpContext,
                tenantRoute,
                tenantDescriptor,
                mediator,
                propertyBag),
            cancellationToken);

        var baseAddress = await mediator.SendAsync<GetTenantBaseAddressCommand, UriDescriptor>(
            new GetTenantBaseAddressCommand(
                httpContext,
                tenantRoute,
                tenantDescriptor,
                tenantSettings,
                mediator,
                propertyBag),
            cancellationToken);

        var issuer = await mediator.SendAsync<GetTenantIssuerCommand, string>(
            new GetTenantIssuerCommand(
                httpContext,
                baseAddress,
                tenantDescriptor,
                tenantSettings,
                mediator,
                propertyBag),
            cancellationToken);

        var secretKeyProvider = await mediator.SendAsync<GetTenantSecretsCommand, ISecretKeyProvider>(
            new GetTenantSecretsCommand(
                httpContext,
                tenantDescriptor,
                tenantSettings,
                mediator,
                propertyBag),
            cancellationToken);

        httpContext.Response.RegisterForDispose(secretKeyProvider);

        return new DefaultOpenIdTenant(
            tenantDescriptor,
            issuer,
            baseAddress,
            tenantSettings,
            secretKeyProvider);
    }

    #region GetTenantDescriptorCommand

    /// <inheritdoc />
    public async ValueTask<TenantDescriptor> HandleAsync(
        GetTenantDescriptorCommand command,
        CancellationToken cancellationToken)
    {
        return ServerOptions.Tenant.Mode switch
        {
            TenantMode.StaticSingle => GetTenantDescriptorFromOptions(),
            TenantMode.DynamicByHost => await GetTenantDescriptorFromHostAsync(command, cancellationToken),
            TenantMode.DynamicByPath => await GetTenantDescriptorFromPathAsync(command, cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported TenantMode: {ServerOptions.Tenant.Mode}")
        };
    }

    private static InvalidOperationException MissingTenantOptionsException(TenantMode mode) =>
        new($"The TenantMode is '{mode}' but the corresponding options are missing.");

    private TenantDescriptor GetTenantDescriptorFromOptions()
    {
        var options = ServerOptions.Tenant.StaticSingle;
        if (options is null)
            throw MissingTenantOptionsException(TenantMode.StaticSingle);

        var tenantId = options.TenantId;
        if (string.IsNullOrEmpty(tenantId))
            tenantId = StaticSingleOpenIdTenantOptions.DefaultTenantId;

        var displayName = options.DisplayName;
        if (string.IsNullOrEmpty(displayName))
            displayName = StaticSingleOpenIdTenantOptions.DefaultDisplayName;

        return new TenantDescriptor
        {
            TenantId = tenantId,
            DisplayName = displayName
        };
    }

    private async ValueTask<TenantDescriptor> GetTenantDescriptorFromHostAsync(
        GetTenantDescriptorCommand command,
        CancellationToken cancellationToken)
    {
        var options = ServerOptions.Tenant.DynamicByHost;
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

        var tenant = await GetTenantByDomainAsync(domainName, cancellationToken);

        command.PropertyBag.Set(tenant);

        return new TenantDescriptor
        {
            TenantId = tenant.TenantId,
            DisplayName = tenant.DisplayName,
            DomainName = tenant.DomainName
        };
    }

    private async ValueTask<TenantDescriptor> GetTenantDescriptorFromPathAsync(
        GetTenantDescriptorCommand command,
        CancellationToken cancellationToken)
    {
        var options = ServerOptions.Tenant.DynamicByPath;
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

        var tenant = await GetTenantByIdAsync(tenantId, cancellationToken);

        command.PropertyBag.Set(tenant);

        return new TenantDescriptor
        {
            TenantId = tenant.TenantId,
            DisplayName = tenant.DisplayName,
            DomainName = tenant.DomainName
        };
    }

    #endregion

    #region GetTenantSettingsCommand

    /// <inheritdoc />
    public async ValueTask<ISettingCollection> HandleAsync(GetTenantSettingsCommand command, CancellationToken cancellationToken)
    {
        if (command.PropertyBag.TryGet<Tenant>(out var tenant))
        {
            return ServerSettingsProvider.Settings.Merge(tenant.Settings);
        }

        if (ServerOptions.Tenant.Mode == TenantMode.StaticSingle)
        {
            return ServerSettingsProvider.Settings;
        }

        tenant = await GetTenantByIdAsync(command.TenantDescriptor.TenantId, cancellationToken);

        return ServerSettingsProvider.Settings.Merge(tenant.Settings);
    }

    #endregion

    #region GetTenantSecretsCommand

    /// <inheritdoc />
    public async ValueTask<ISecretKeyProvider> HandleAsync(GetTenantSecretsCommand command, CancellationToken cancellationToken)
    {
        if (command.PropertyBag.TryGet<Tenant>(out var tenant))
        {
            return DeserializeSecrets(tenant.Secrets);
        }

        if (ServerOptions.Tenant.Mode == TenantMode.StaticSingle)
        {
            var serviceProvider = command.HttpContext.RequestServices;
            return serviceProvider.GetRequiredService<ISecretKeyProvider>();
        }

        tenant = await GetTenantByIdAsync(command.TenantDescriptor.TenantId, cancellationToken);

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
                basePath = basePath.Add(tenantRouteUrl);
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
        var tenantSettings = command.TenantSettings;
        if (tenantSettings.TryGet(new SettingKey<string>(SettingNames.TenantIssuer), out var setting) &&
            !string.IsNullOrEmpty(setting.Value))
        {
            return ValueTask.FromResult(setting.Value);
        }

        return ValueTask.FromResult(command.BaseAddress.ToString());
    }

    #endregion
}
