// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Management.Contracts.Capabilities;
using NCode.Identity.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Management.Endpoints.Capabilities;

/*

GET api/capabilities

*/

/// <summary>
/// Provides the server-wide management capabilities discovery endpoint, letting management clients discover server
/// behavior (such as the supported secret types and which expose a public key) instead of hard-coding it. The
/// document is composed from the registered <see cref="DiscoverManagementCapabilitiesCommand"/> handlers, so new
/// capabilities are exposed by registering a provider rather than by changing this endpoint. It answers only a
/// caller that the management authentication filter authenticated from a management access token, which it reads
/// from <see cref="IManagementCallerFeature"/>.
/// </summary>
// See ADR-1067 and ADR-1044.
internal class CapabilitiesApiEndpointHandler : IEndpointProvider
{
    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/capabilities", GetCapabilitiesAsync)
            .WithTags(OpenIdConstants.EndpointTags.Capabilities)
            .Produces<ManagementCapabilitiesResource>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
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
        if (httpContext.Features.Get<IManagementCallerFeature>() is null)
            return ManagementProblems.AuthenticationRequired();

        var openIdContext = httpContext.GetOpenIdContext();

        var result = new ManagementCapabilitiesResource();
        await openIdContext.Mediator.SendAsync(
            new DiscoverManagementCapabilitiesCommand(openIdContext, result.Capabilities),
            cancellationToken
        );

        return TypedResults.Json(result);
    }
}
