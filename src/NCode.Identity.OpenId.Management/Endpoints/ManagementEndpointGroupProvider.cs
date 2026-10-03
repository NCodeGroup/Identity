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

using System.Collections.Immutable;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NCode.Identity.Endpoints;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// An <see cref="IEndpointGroupProvider"/> that maps every management API endpoint family into the shared
/// top-level <c>/api</c> route group (so the management surface is served under <c>/api/clients</c>,
/// <c>/api/servers</c>, <c>/api/tenants</c>, …), distinct from the OpenID protocol endpoints at the root.
/// </summary>
internal sealed class ManagementEndpointGroupProvider(
    IEnumerable<IManagementEndpointProvider> endpointProviders
) : IEndpointGroupProvider
{
    private ImmutableArray<IManagementEndpointProvider> EndpointProviders { get; } =
    [.. endpointProviders];

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints)
    {
        // Materialize the OpenID request environment for every management endpoint so each can bind the OpenIdContext;
        // the ambient tenant data-scope is opened per tenant-scoped subgroup (AmbientTenantScopeEndpointFilter).
        var group = endpoints.MapGroup("/api").AddEndpointFilter<OpenIdEnvironmentEndpointFilter>();

        foreach (var endpointProvider in EndpointProviders)
        {
            endpointProvider.Map(group);
        }
    }
}
