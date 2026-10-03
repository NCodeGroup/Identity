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

using NCode.Identity.Endpoints;
using NCode.Identity.OpenId.Management.Endpoints.ResourceServers;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// The client-plane management route group (<c>/api/tenants/{tenantId}/clients/{clientId}</c>), nested under the
/// tenant-plane group. It carries a client's child families (its grants) so they map only their own relative
/// sub-routes while inheriting the tenant scope from the parent group (ADR-0042).
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
    public string Prefix => "/clients/{clientId}";

    /// <inheritdoc />
    public void Build(IEndpointGroupBuilder builder)
    {
        builder.AddEndpoint<ClientGrantApiEndpointHandler>();
    }
}
