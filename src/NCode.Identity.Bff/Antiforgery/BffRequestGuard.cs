// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.Bff.Options;

namespace NCode.Identity.Bff.Antiforgery;

/// <summary>
/// Applies the static anti-forgery header and the Fetch Metadata checks to a request.
/// </summary>
internal static class BffRequestGuard
{
    /// <summary>
    /// The reason returned when the anti-forgery header is missing, repeated or wrong.
    /// </summary>
    public const string AntiforgeryHeaderMissing = "antiforgery_header_missing";

    /// <summary>
    /// The reason returned when Fetch Metadata shows a cross-site request.
    /// </summary>
    public const string CrossSiteRequest = "cross_site_request";

    /// <summary>
    /// The reason returned when a request to a page uses a method other than GET or HEAD.
    /// </summary>
    public const string UnsafeMethod = "unsafe_method";

    private const string SecFetchSiteHeaderName = "Sec-Fetch-Site";
    private const string SameOrigin = "same-origin";
    private const string None = "none";
    private const string SameSite = "same-site";

    /// <summary>
    /// Checks a request to a BFF-protected endpoint.
    /// </summary>
    /// <param name="request">The request to check.</param>
    /// <param name="antiforgery">The anti-forgery options.</param>
    /// <param name="trustedOrigins">
    /// The origins of the request's frontend that may send same-site requests.
    /// </param>
    /// <returns>
    /// <c>null</c> when the request passes; otherwise the reason it is refused.
    /// </returns>
    public static string? Check(
        HttpRequest request,
        BffAntiforgeryOptions antiforgery,
        IReadOnlyList<string> trustedOrigins
    )
    {
        var headerName = antiforgery.HeaderName;
        var headerValues = request.Headers[headerName];
        if (
            headerValues.Count != 1
            || !string.Equals(headerValues[0], antiforgery.HeaderValue, StringComparison.Ordinal)
        )
        {
            return AntiforgeryHeaderMissing;
        }

        return CheckFetchMetadata(request, trustedOrigins);
    }

    /// <summary>
    /// Checks the Fetch Metadata of a request: a same-origin or user-initiated request passes, and a same-site request
    /// passes only from a trusted origin.
    /// </summary>
    /// <param name="request">The request to check.</param>
    /// <param name="trustedOrigins">
    /// The origins of the request's frontend that may send same-site requests.
    /// </param>
    /// <returns>
    /// <c>null</c> when the request passes; otherwise the reason it is refused.
    /// </returns>
    public static string? CheckFetchMetadata(
        HttpRequest request,
        IReadOnlyList<string> trustedOrigins
    )
    {
        var site = request.Headers[SecFetchSiteHeaderName];
        if (site.Count == 0)
            return null;

        if (site.Count != 1)
            return CrossSiteRequest;

        var siteValue = site[0];
        if (
            string.Equals(siteValue, SameOrigin, StringComparison.Ordinal)
            || string.Equals(siteValue, None, StringComparison.Ordinal)
        )
        {
            return null;
        }

        if (string.Equals(siteValue, SameSite, StringComparison.Ordinal))
        {
            var origin = request.Headers.Origin;
            if (origin.Count == 1 && origin[0] is { } originValue)
            {
                foreach (var trusted in trustedOrigins)
                {
                    if (string.Equals(trusted, originValue, StringComparison.Ordinal))
                        return null;
                }
            }
        }

        return CrossSiteRequest;
    }

    /// <summary>
    /// Checks a request to a BFF hub: the Fetch Metadata check applies, and an <c>Origin</c> header, when sent, must be
    /// the frontend's public origin or a trusted origin because a browser <c>WebSocket</c> cannot send the
    /// anti-forgery header.
    /// </summary>
    /// <param name="request">The request to check.</param>
    /// <param name="publicOrigin">The public origin of the request's frontend.</param>
    /// <param name="trustedOrigins">
    /// The origins of the request's frontend that may send same-site requests.
    /// </param>
    /// <returns>
    /// <c>null</c> when the request passes; otherwise the reason it is refused.
    /// </returns>
    public static string? CheckHub(
        HttpRequest request,
        string? publicOrigin,
        IReadOnlyList<string> trustedOrigins
    )
    {
        var refusal = CheckFetchMetadata(request, trustedOrigins);
        if (refusal is not null)
            return refusal;

        var origin = request.Headers.Origin;
        if (origin.Count == 0)
            return null;

        if (origin.Count != 1 || origin[0] is not { } originValue)
            return CrossSiteRequest;

        if (string.Equals(publicOrigin, originValue, StringComparison.Ordinal))
            return null;

        foreach (var trusted in trustedOrigins)
        {
            if (string.Equals(trusted, originValue, StringComparison.Ordinal))
                return null;
        }

        return CrossSiteRequest;
    }

    /// <summary>
    /// Checks a request to a BFF page, which accepts only the safe methods GET and HEAD because it carries no
    /// anti-forgery header.
    /// </summary>
    /// <param name="request">The request to check.</param>
    /// <returns>
    /// <c>null</c> when the request passes; otherwise the reason it is refused.
    /// </returns>
    public static string? CheckPage(HttpRequest request)
    {
        var method = request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method))
            return null;

        return UnsafeMethod;
    }
}
