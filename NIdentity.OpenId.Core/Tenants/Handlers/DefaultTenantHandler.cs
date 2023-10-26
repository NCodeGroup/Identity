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
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.Options;
using NIdentity.OpenId.DataContracts;
using NIdentity.OpenId.Mediator;
using NIdentity.OpenId.Options;
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
    private IMediator Mediator { get; }
    private ITenantStore TenantStore { get; }
    private TemplateBinderFactory TemplateBinderFactory { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTenantHandler"/> class.
    /// </summary>
    public DefaultTenantHandler(
        IOptions<OpenIdHostOptions> hostOptionsAccessor,
        IMediator mediator,
        ITenantStore tenantStore,
        TemplateBinderFactory templateBinderFactory)
    {
        HostOptions = hostOptionsAccessor.Value;
        Mediator = mediator;
        TenantStore = tenantStore;
        TemplateBinderFactory = templateBinderFactory;
    }

    /// <inheritdoc />
    public async ValueTask<OpenIdTenant> HandleAsync(GetOpenIdTenantCommand command, CancellationToken cancellationToken)
    {
        var (httpContext, tenantRoute, propertyBag) = command;

        var configuration = await Mediator.SendAsync(
            new GetTenantConfigurationCommand(
                httpContext,
                tenantRoute,
                propertyBag),
            cancellationToken);

        var baseAddress = await Mediator.SendAsync(
            new GetTenantBaseAddressCommand(
                httpContext,
                tenantRoute,
                configuration,
                propertyBag),
            cancellationToken);

        var issuer = await Mediator.SendAsync(
            new GetTenantIssuerCommand(
                httpContext,
                baseAddress,
                configuration,
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
            _ => throw new InvalidOperationException($"Unsupported tenant mode: {HostOptions.Tenant.Mode}")
        };
    }

    private static Exception MissingTenantOptionsException(TenantMode mode) =>
        new InvalidOperationException($"Tenant Mode is {mode} but the corresponding options are missing.");

    private TenantConfiguration GetTenantFromOptions()
    {
        var options = HostOptions.Tenant.StaticSingle;
        if (options == null)
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
        if (options == null)
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
            throw new InvalidOperationException();

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

        // TODO better exception/message

        if (tenantRoute == null)
            throw new InvalidOperationException();

        if (tenantRoute.Parameters.Count == 0)
            throw new InvalidOperationException();

        var httpRequest = httpContext.Request;
        var routeValues = httpRequest.RouteValues;

        if (!routeValues.TryGetValue(options.TenantIdRouteParameterName, out var routeValue))
            throw new InvalidOperationException();

        if (routeValue is not string tenantId)
            throw new InvalidOperationException();

        if (string.IsNullOrEmpty(tenantId))
            throw new InvalidOperationException();

        var tenant = await TenantStore.TryGetByTenantIdAsync(tenantId, cancellationToken);
        if (tenant == null)
            throw new InvalidOperationException();

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
