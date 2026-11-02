// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using NCode.Identity.OpenId.Management.Endpoints.Capabilities;
using NCode.Identity.OpenId.Management.Endpoints.Servers;
using NCode.Identity.OpenId.Management.Endpoints.Tenants;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// The root management route group (<c>/api</c>). It materializes the OpenID request environment for every management
/// endpoint via <see cref="ManagementEnvironmentEndpointFilter"/> but opens no tenant scope, so control-plane
/// families (servers, tenant provisioning) act on unscoped rows.
/// </summary>
internal sealed class ManagementRootGroup : IEndpointGroup
{
    /// <summary>
    /// The <see cref="IEndpointGroup.Name"/> of the root management group.
    /// </summary>
    public const string GroupName = "api";

    /// <inheritdoc />
    public string Name => GroupName;

    /// <inheritdoc />
    public RoutePattern GetPrefix(IServiceProvider serviceProvider) =>
        RoutePatternFactory.Parse("/api");

    /// <inheritdoc />
    public void ConfigureRoutes(RouteGroupBuilder group) =>
        group
            .AddEndpointFilter<RouteGroupBuilder, ManagementEnvironmentEndpointFilter>()
            .AddEndpointFilter<RouteGroupBuilder, ManagementAuthenticationEndpointFilter>()
            .AddEndpointFilter<RouteGroupBuilder, StoreConcurrencyEndpointFilter>()
            .AddEndpointFilter<RouteGroupBuilder, UnreadableSettingsEndpointFilter>();

    /// <inheritdoc />
    public void ConfigureServices(IEndpointGroupBuilder builder)
    {
        builder.AddCapabilitiesEndpoint();
        builder.AddServerEndpoint();
        builder.AddTenantEndpoint();
        builder.AddGroup<TenantManagementGroup>();
    }
}
