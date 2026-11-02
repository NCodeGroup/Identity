// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using NCode.Identity.OpenId.Management.Endpoints.AuditRecords;
using NCode.Identity.OpenId.Management.Endpoints.Clients;
using NCode.Identity.OpenId.Management.Endpoints.Grants;
using NCode.Identity.OpenId.Management.Endpoints.Principals;
using NCode.Identity.OpenId.Management.Endpoints.ResourceServers;
using NCode.Identity.OpenId.Management.Endpoints.Sessions;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// The tenant-plane management route group (<c>/api/tenants/{tenantId}</c>). It opens the ambient tenant scope from the
/// route's <c>tenantId</c> via <see cref="TenantScopeEndpointFilter"/>, so every tenant-bound family nested under it
/// (clients, grants, resource servers) is confined to the addressed tenant fail-closed. The family handlers
/// map only their own relative sub-routes; the tenant prefix lives here.
/// </summary>
internal sealed class TenantManagementGroup : IEndpointGroup
{
    /// <summary>
    /// The <see cref="IEndpointGroup.Name"/> of the tenant-plane management group.
    /// </summary>
    public const string GroupName = ManagementEndpointGroups.Tenant;

    /// <inheritdoc />
    public string Name => GroupName;

    /// <inheritdoc />
    public RoutePattern GetPrefix(IServiceProvider serviceProvider) =>
        RoutePatternFactory.Parse("/tenants/{tenantId}");

    /// <inheritdoc />
    public void ConfigureRoutes(RouteGroupBuilder group) =>
        group.AddEndpointFilter<RouteGroupBuilder, TenantScopeEndpointFilter>();

    /// <inheritdoc />
    public void ConfigureServices(IEndpointGroupBuilder builder)
    {
        builder.AddClientEndpoint();
        builder.AddGrantEndpoint();
        builder.AddAuditRecordEndpoint();
        builder.AddResourceServerEndpoint();
        builder.AddSessionEndpoint();
        builder.AddPrincipalEndpoint();
        builder.AddGroup<ClientManagementGroup>();
    }
}
