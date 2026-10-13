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
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Management.Contracts.Capabilities;
using NCode.Mediator;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Management.Endpoints.Capabilities;

/*

GET api/capabilities

*/

/// <summary>
/// Provides the server-wide management capabilities discovery endpoint, letting management clients discover server
/// behavior (such as the supported secret types and which expose a public key) instead of hard-coding it. The
/// document is composed from the registered <see cref="DiscoverManagementCapabilitiesCommand"/> handlers, so new
/// capabilities are exposed by registering a provider rather than by changing this endpoint.
/// </summary>
// See ADR-0067.
internal class CapabilitiesApiEndpointHandler : IEndpointProvider
{
    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/capabilities", GetCapabilitiesAsync)
            .WithTags(OpenIdConstants.EndpointTags.Capabilities)
            .Produces<ManagementCapabilitiesResource>();
    }

    /// <summary>
    /// Gets the server-wide management capabilities.
    /// </summary>
    [EndpointName("api/capabilities/get")]
    internal virtual async ValueTask<IResult> GetCapabilitiesAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var openIdContext = httpContext.GetOpenIdContext();

        var result = new ManagementCapabilitiesResource();
        await openIdContext.Mediator.SendAsync(
            new DiscoverManagementCapabilitiesCommand(openIdContext, result.Capabilities),
            cancellationToken
        );

        return TypedResults.Json(result);
    }
}
