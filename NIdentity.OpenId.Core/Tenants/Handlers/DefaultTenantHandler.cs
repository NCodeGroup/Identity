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

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NCode.Jose.SecretKeys;
using NIdentity.OpenId.Mediator;
using NIdentity.OpenId.Options;
using NIdentity.OpenId.Stores;
using NIdentity.OpenId.Tenants.Commands;

namespace NIdentity.OpenId.Tenants.Handlers;

internal class DefaultTenantHandler :
    ICommandResponseHandler<GetOpenIdTenantCommand, OpenIdTenant>,
    ICommandResponseHandler<GetTenantIdCommand, string>,
    ICommandResponseHandler<GetTenantIssuerCommand, string>,
    ICommandResponseHandler<GetTenantBaseAddressCommand, UriDescriptor>
{
    private OpenIdHostOptions HostOptions { get; }
    private IMediator Mediator { get; }
    private ITenantStore TenantStore { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTenantHandler"/> class.
    /// </summary>
    public DefaultTenantHandler(
        IOptions<OpenIdHostOptions> hostOptionsAccessor,
        IMediator mediator,
        ITenantStore tenantStore)
    {
        HostOptions = hostOptionsAccessor.Value;
        Mediator = mediator;
        TenantStore = tenantStore;
    }

    /// <inheritdoc />
    public async ValueTask<OpenIdTenant> HandleAsync(GetOpenIdTenantCommand command, CancellationToken cancellationToken) =>
        HostOptions.Tenant.Mode switch
        {
            TenantMode.Static => await GetStaticSingleTenantAsync(command.HttpContext, cancellationToken),
            TenantMode.DynamicByPath => await GetDynamicByPathTenantAsync(command.HttpContext, cancellationToken),
            TenantMode.DynamicByHost => await GetDynamicByHostTenantAsync(command.HttpContext, cancellationToken),
            _ => throw new InvalidOperationException("Invalid tenancy mode.")
        };

    // var tenantId = await Mediator.SendAsync(new GetTenantIdCommand(httpContext), cancellationToken);
    // if (string.IsNullOrEmpty(tenantId))
    //     // TODO: better exception
    //     throw new InvalidOperationException();
    //
    // var tenant = await TenantStore.TryGetByTenantIdAsync(tenantId, cancellationToken);
    // if (tenant == null)
    //     // TODO: better exception
    //     throw new InvalidOperationException();
    //
    // var issuer = await Mediator.SendAsync(new GetTenantIssuerCommand(httpContext, tenant), cancellationToken);
    // var baseAddress = await Mediator.SendAsync(new GetTenantBaseAddressCommand(httpContext, tenant), cancellationToken);
    //
    // return new DefaultOpenIdTenant(tenant, issuer, baseAddress);

    private ValueTask<OpenIdTenant> GetStaticSingleTenantAsync(HttpContext httpContext, CancellationToken cancellationToken)
    {
        var options = HostOptions.Tenant.StaticSingle;

        var tenantId = options.TenantId;
        if (string.IsNullOrEmpty(tenantId))
            tenantId = StaticSingleOpenIdTenantOptions.DefaultTenantId;

        var displayName = options.DisplayName;
        if (string.IsNullOrEmpty(displayName))
            displayName = StaticSingleOpenIdTenantOptions.DefaultDisplayName;

        var basePath = options.BasePath;
        if (!string.IsNullOrEmpty(basePath) && basePath[0] != '/')
            basePath = '/' + basePath;

        var request = httpContext.Request;
        var baseAddress = new UriDescriptor
        {
            Scheme = request.Scheme,
            Host = request.Host,
            Path = request.PathBase.Add(basePath)
        };

        var issuer = options.Issuer;
        if (string.IsNullOrEmpty(issuer))
            issuer = baseAddress.ToString();

        var endpointBasePath = HostOptions.EndpointBasePath;
        if (string.IsNullOrEmpty(endpointBasePath))
            endpointBasePath = "/";
        else if (endpointBasePath[0] != '/')
            endpointBasePath = '/' + endpointBasePath;

        var endpointBaseRoute = RoutePatternFactory.Parse(endpointBasePath);

        var descriptor = new OpenIdTenantDescriptor
        {
            TenantId = tenantId,
            DisplayName = displayName,
            Issuer = issuer,
            BaseAddress = baseAddress,
            EndpointBaseRoute = endpointBaseRoute,
            TenantIdRouteParameterName = null
        };

        var secretKeyProvider = httpContext.RequestServices.GetRequiredService<ISecretKeyProvider>();

        var tenant = new StaticOpenIdTenant(
            descriptor,
            secretKeyProvider,
            options.Configuration);

        return ValueTask.FromResult<OpenIdTenant>(tenant);
    }

    private async ValueTask<OpenIdTenant> GetDynamicByPathTenantAsync(HttpContext httpContext, CancellationToken cancellationToken)
    {
        var options = HostOptions.Tenant.DynamicByPath;

        // RoutePatternFactory.Combine();

        var routePattern = RoutePatternFactory.Parse(options.RoutePattern);

        throw new NotImplementedException();
    }

    private async ValueTask<OpenIdTenant> GetDynamicByHostTenantAsync(HttpContext httpContext, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public ValueTask<string> HandleAsync(GetTenantIdCommand command, CancellationToken cancellationToken)
    {
        var httpContext = command.HttpContext;

        // EnableMultiTenancy: true, false
        // TenantDiscoveryMode: none, path, host

        // examples:
        //
        // https://anything.com/oauth2/token
        // - TenantId=tenantid
        // - BaseAddress=https://anything.com
        // - Issuer=https://something.com
        //
        // https://tenantid.nauth0.com/oauth2/token
        // - TenantId=tenantid
        // - BaseAddress=https://tenantid.nauth0.com
        // - Issuer=https://tenantid.nauth0.com
        //
        // https://login.nauth0.com/tenantid/oauth2/token
        // - TenantId=tenantid
        // - BaseAddress=https://login.nauth0.com/tenantid
        // - Issuer=https://login.nauth0.com/tenantid

        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public ValueTask<string> HandleAsync(GetTenantIssuerCommand command, CancellationToken cancellationToken)
    {
        var (httpContext, tenant) = command;

        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public ValueTask<UriDescriptor> HandleAsync(GetTenantBaseAddressCommand command, CancellationToken cancellationToken)
    {
        var (httpContext, tenant) = command;

        var request = httpContext.Request;
        var fullPath = GetFullPath(request.PathBase, request.Path);

        var uriDescriptor = new UriDescriptor
        {
            Scheme = request.Scheme,
            Host = request.Host,
            Path = fullPath
        };

        return ValueTask.FromResult(uriDescriptor);
    }

    private static PathString GetFullPath(PathString basePath, PathString path) =>
        basePath.HasValue && (basePath.Value.Length > 1 || basePath.Value[0] != '/') ?
            basePath.Add(path) :
            path;
}
