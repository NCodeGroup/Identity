// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Routing.Patterns;
using NCode.Identity.OpenId.Management.Endpoints.ResourceServers;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// The client-plane management route group (<c>/api/tenants/{tenantId}/clients/{clientId}</c>), nested under the
/// tenant-plane group. It carries a client's child families (its grants) so they map only their own relative
/// sub-routes while inheriting the tenant scope from the parent group.
/// </summary>
internal sealed class ClientManagementGroup : IEndpointGroup
{
    /// <summary>
    /// The <see cref="IEndpointGroup.Name"/> of the client-plane management group.
    /// </summary>
    public const string GroupName = "api/tenant/client";

    /// <inheritdoc />
    public string Name => GroupName;

    /// <inheritdoc />
    public RoutePattern GetPrefix(IServiceProvider serviceProvider) =>
        RoutePatternFactory.Parse("/clients/{clientId}");

    /// <inheritdoc />
    public void ConfigureServices(IEndpointGroupBuilder builder)
    {
        builder.AddClientGrantEndpoint();
    }
}
