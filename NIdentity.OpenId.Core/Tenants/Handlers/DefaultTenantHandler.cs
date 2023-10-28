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
using Microsoft.Extensions.Options;
using NIdentity.OpenId.DataContracts;
using NIdentity.OpenId.Mediator;
using NIdentity.OpenId.Options;
using NIdentity.OpenId.Results;
using NIdentity.OpenId.Stores;
using NIdentity.OpenId.Tenants.Commands;

namespace NIdentity.OpenId.Tenants.Handlers;

internal class DefaultTenantHandler :
    ICommandResponseHandler<GetOpenIdTenantCommand, OpenIdTenant>,
    ICommandResponseHandler<GetTenantConfigurationCommand, TenantConfiguration>,
    ICommandResponseHandler<GetTenantBaseAddressCommand, UriDescriptor>,
    ICommandResponseHandler<GetTenantIssuerCommand, string>
{
    private Regex? DomainNameRegex { get; set; }
    private OpenIdHostOptions HostOptions { get; }
    private ITenantStore TenantStore { get; }
    private TemplateBinderFactory TemplateBinderFactory { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTenantHandler"/> class.
    /// </summary>
    public DefaultTenantHandler(
        IOptions<OpenIdHostOptions> hostOptionsAccessor,
        ITenantStore tenantStore,
        TemplateBinderFactory templateBinderFactory)
    {
        HostOptions = hostOptionsAccessor.Value;
        TenantStore = tenantStore;
        TemplateBinderFactory = templateBinderFactory;
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

        return new DefaultOpenIdTenant(configuration, baseAddress, issuer);
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
        if (tenant == null)
            throw TypedResults.NotFound().AsException($"A tenant with domain '{domainName}' could not be found.");

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
        if (tenant == null)
            throw TypedResults.NotFound().AsException($"A tenant with identifier '{tenantId}' could not be found.");

        return tenant.Configuration;
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
