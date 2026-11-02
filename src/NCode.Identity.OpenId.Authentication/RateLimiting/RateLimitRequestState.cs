// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;

namespace NCode.Identity.OpenId.Authentication.RateLimiting;

/// <summary>
/// Holds what the request rate limiter records on one request: whether a stage refused it, and the usage meters of
/// every stage that admitted it. <see cref="RateLimitEndpointFilter"/> reads it once the endpoint has run, and records
/// the meters unless a stage refused the request. The state lives in <see cref="HttpContext.Items"/>, so it ends with
/// the request.
/// </summary>
// See ADR-1246.
internal sealed class RateLimitRequestState
{
    /// <summary>
    /// Gets or sets a value indicating whether a stage refused the request.
    /// </summary>
    public bool IsRefused { get; set; }

    /// <summary>
    /// Gets the names of the usage meters that the stages admitting the request added, each once, in the order they
    /// were first added.
    /// </summary>
    public List<string> UsageMeters { get; } = [];

    private static object ItemKey { get; } = new();

    /// <summary>
    /// Gets the state of a request, storing an empty state on the request first when it has none.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> of the request.</param>
    /// <returns>The <see cref="RateLimitRequestState"/> of the request.</returns>
    public static RateLimitRequestState Get(HttpContext httpContext)
    {
        if (
            httpContext.Items.TryGetValue(ItemKey, out var stored)
            && stored is RateLimitRequestState existing
        )
            return existing;

        var state = new RateLimitRequestState();
        httpContext.Items[ItemKey] = state;
        return state;
    }
}
