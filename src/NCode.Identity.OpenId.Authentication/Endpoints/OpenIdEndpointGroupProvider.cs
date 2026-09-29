#region Copyright Preamble

// Copyright @ 2025 NCode Group
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

namespace NCode.Identity.OpenId.Authentication.Endpoints;

/// <summary>
/// An <see cref="IEndpointGroupProvider"/> that maps all OpenID endpoints into a shared route group and
/// installs the <see cref="OpenIdExceptionEndpointFilter"/> so that exceptions thrown by any OpenID endpoint
/// are rendered as standard OpenID error responses.
/// </summary>
internal sealed class OpenIdEndpointGroupProvider(
    IEnumerable<IOpenIdEndpointProvider> endpointProviders
) : IEndpointGroupProvider
{
    private ImmutableArray<IOpenIdEndpointProvider> EndpointProviders { get; } =
    [.. endpointProviders];

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints)
    {
        // An empty prefix preserves each endpoint's absolute route (e.g. /oauth2/token) while still
        // grouping them so the exception filter is scoped to OpenID endpoints only.
        var group = endpoints.MapGroup(string.Empty);

        group.AddEndpointFilter<RouteGroupBuilder, OpenIdExceptionEndpointFilter>();

        foreach (var endpointProvider in EndpointProviders)
        {
            endpointProvider.Map(group);
        }
    }
}
