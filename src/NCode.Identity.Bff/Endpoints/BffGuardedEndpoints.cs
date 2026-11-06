// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;

namespace NCode.Identity.Bff.Endpoints;

// The BFF cookie authenticates only these endpoints (ADR-1292).

/// <summary>
/// Classifies the endpoints whose requests the BFF cookie may authenticate.
/// </summary>
internal static class BffGuardedEndpoints
{
    /// <summary>
    /// Determines whether the endpoint is an API marked <c>[BffApi]</c> or <c>AsBffApi()</c>: a challenge answers 401
    /// and a request slides the session.
    /// </summary>
    /// <param name="endpoint">The endpoint of the request, if any.</param>
    public static bool IsApi(Endpoint? endpoint) =>
        endpoint?.Metadata.GetMetadata<BffApiAttribute>() is not null;

    /// <summary>
    /// Determines whether the endpoint is an API or <c>/bff/user</c>: the anti-forgery header and Fetch Metadata are
    /// required.
    /// </summary>
    /// <param name="endpoint">The endpoint of the request, if any.</param>
    public static bool RequiresAntiforgery(Endpoint? endpoint) =>
        IsApi(endpoint) || endpoint?.Metadata.GetMetadata<BffUserEndpointMetadata>() is not null;

    /// <summary>
    /// Determines whether the endpoint is a SignalR hub marked <c>[BffHub]</c> or <c>AsBffHub()</c> and not an API: any
    /// method, the Fetch Metadata and <c>Origin</c> checks apply, a challenge answers 401 and a request slides the
    /// session. An API outranks a hub.
    /// </summary>
    /// <param name="endpoint">The endpoint of the request, if any.</param>
    public static bool IsHub(Endpoint? endpoint) =>
        !RequiresAntiforgery(endpoint)
        && endpoint?.Metadata.GetMetadata<BffHubAttribute>() is not null;

    /// <summary>
    /// Determines whether the endpoint is a page marked <c>[BffPage]</c> or <c>AsBffPage()</c> and neither an API nor a
    /// hub: GET and HEAD only, a challenge redirects to login. An API and a hub outrank a page.
    /// </summary>
    /// <param name="endpoint">The endpoint of the request, if any.</param>
    public static bool IsPage(Endpoint? endpoint) =>
        !RequiresAntiforgery(endpoint)
        && !IsHub(endpoint)
        && endpoint?.Metadata.GetMetadata<BffPageAttribute>() is not null;

    /// <summary>
    /// Determines whether the endpoint is one the BFF cookie may authenticate.
    /// </summary>
    /// <param name="endpoint">The endpoint of the request, if any.</param>
    public static bool IsGuarded(Endpoint? endpoint) =>
        RequiresAntiforgery(endpoint) || IsHub(endpoint) || IsPage(endpoint);
}
