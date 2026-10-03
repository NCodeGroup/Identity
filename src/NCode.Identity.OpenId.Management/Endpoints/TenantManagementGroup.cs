#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NCode.Identity.Endpoints;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// The tenant-plane management route group (<c>/api/tenants/{tenantId}</c>). It opens the ambient tenant scope from the
/// route's <c>tenantId</c> via <see cref="TenantScopeEndpointFilter"/>, so every tenant-bound family nested under it
/// (clients, grants, resource servers) is confined to the addressed tenant fail-closed (ADR-0042). The family handlers
/// map only their own relative sub-routes; the tenant prefix lives here.
/// </summary>
internal sealed class TenantManagementGroup : IEndpointGroup
{
    /// <summary>
    /// The <see cref="IEndpointGroup.Name"/> of the tenant-plane management group.
    /// </summary>
    public const string GroupName = "api/tenant";

    /// <inheritdoc />
    public string Name => GroupName;

    /// <inheritdoc />
    public string? ParentName => ManagementRootGroup.GroupName;

    /// <inheritdoc />
    public string Prefix => "/tenants/{tenantId}";

    /// <inheritdoc />
    public void Configure(RouteGroupBuilder group) =>
        group.AddEndpointFilter<RouteGroupBuilder, TenantScopeEndpointFilter>();
}
