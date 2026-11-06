// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Matching;

namespace NCode.Identity.Bff.Frontends;

/// <summary>
/// Keeps only the candidate endpoints that serve the frontend that a request selects.
/// </summary>
/// <remarks>
/// An endpoint that carries <see cref="BffFrontendAttribute"/> metadata is a candidate only for the frontends it
/// names. An endpoint without that metadata serves whichever frontend the request selects.
/// </remarks>
internal sealed class BffFrontendMatcherPolicy(BffFrontendCatalog frontends)
    : MatcherPolicy,
        IEndpointSelectorPolicy
{
    private static bool Names(IReadOnlyList<BffFrontendAttribute> attributes, string name)
    {
        foreach (var attribute in attributes)
        {
            if (string.Equals(attribute.Name, name, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <inheritdoc />
    public override int Order => 0;

    /// <inheritdoc />
    public bool AppliesToEndpoints(IReadOnlyList<Endpoint> endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        foreach (var endpoint in endpoints)
        {
            if (endpoint.Metadata.GetMetadata<BffFrontendAttribute>() is not null)
                return true;
        }

        return false;
    }

    /// <inheritdoc />
    public Task ApplyAsync(HttpContext httpContext, CandidateSet candidates)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(candidates);

        // Frontends that share a route pattern are told apart by the request's frontend, not by routing.
        var resolved = false;
        BffFrontend? frontend = null;
        for (var i = 0; i < candidates.Count; i++)
        {
            if (!candidates.IsValidCandidate(i))
                continue;

            var attributes = candidates[i]
                .Endpoint.Metadata.GetOrderedMetadata<BffFrontendAttribute>();
            if (attributes.Count == 0)
                continue;

            if (!resolved)
            {
                frontend = frontends.Resolve(httpContext);
                resolved = true;
            }

            candidates.SetValidity(i, frontend is not null && Names(attributes, frontend.Name));
        }

        return Task.CompletedTask;
    }
}
