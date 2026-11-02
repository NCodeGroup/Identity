// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Endpoints;

/// <summary>
/// Builds the URLs the tenant publishes for the endpoint that is receiving the current request, for the comparisons
/// that bind a client's message to that endpoint: the client-assertion <c>aud</c> (OpenID Connect Core §9) and the
/// DPoP proof's <c>htu</c> (RFC 9449 §4.3).
/// </summary>
/// <remarks>
/// The authority comes from the tenant — its issuer's base address, or its mutual-TLS endpoint-alias authority
/// (RFC 8705 §5) — and only the path comes from the request, which routing already matched to this endpoint, as
/// <see cref="OpenIdLinkGeneratorExtensions"/> does for the URLs the discovery document publishes. The request's
/// <c>Host</c> therefore never widens the values the server accepts: a forged <c>Host</c> chooses at most which of the
/// tenant's own published origins is compared (project-template ADR-0126, ADR-1387).
/// </remarks>
internal static class TenantRequestUri
{
    /// <summary>
    /// Gets every URL the tenant publishes for the endpoint receiving the request: the one under the tenant's base
    /// address, then, when the tenant has a mutual-TLS endpoint-alias authority, the one under that authority.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> for the current request.</param>
    /// <returns>The published URLs of the endpoint receiving the request.</returns>
    public static IReadOnlyList<string> GetPublishedUris(OpenIdContext openIdContext)
    {
        ArgumentNullException.ThrowIfNull(openIdContext);

        var primary = GetBaseAddressUri(openIdContext);
        return GetMtlsAlias(openIdContext) is { } alias ? [primary, alias.Uri] : [primary];
    }

    /// <summary>
    /// Gets the URL the tenant publishes for the endpoint receiving the request at the origin the request was sent to:
    /// the one under the tenant's mutual-TLS endpoint-alias authority when the request was sent to that authority,
    /// otherwise the one under the tenant's base address.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> for the current request.</param>
    /// <returns>The published URL of the endpoint receiving the request.</returns>
    public static string GetPublishedUri(OpenIdContext openIdContext)
    {
        ArgumentNullException.ThrowIfNull(openIdContext);

        if (
            GetMtlsAlias(openIdContext) is { } alias
            && IsSentTo(openIdContext.Http.Request, alias.Authority)
        )
            return alias.Uri;

        return GetBaseAddressUri(openIdContext);
    }

    private static string GetBaseAddressUri(OpenIdContext openIdContext)
    {
        var request = openIdContext.Http.Request;
        var baseAddress = openIdContext.Tenant.BaseAddress;
        return $"{baseAddress.Scheme}://{baseAddress.Host}{request.PathBase}{request.Path}";
    }

    private static (string Uri, Uri Authority)? GetMtlsAlias(OpenIdContext openIdContext)
    {
        if (
            !AbsoluteUri.TryParse(
                openIdContext.Tenant.MtlsEndpointAliasAuthority,
                out var authority
            )
        )
            return null;

        var request = openIdContext.Http.Request;
        return (
            $"{authority.Scheme}://{authority.Authority}{request.PathBase}{request.Path}",
            authority
        );
    }

    private static bool IsSentTo(HttpRequest request, Uri authority)
    {
        var host = request.Host;
        if (!host.HasValue)
            return false;

        // The authority's IdnHost is in punycode, and HostString encodes a Unicode host the same way.
        var requestHost = new HostString(host.Host).ToUriComponent();
        var requestPort = host.Port ?? (request.IsHttps ? 443 : 80);
        return string.Equals(requestHost, authority.IdnHost, StringComparison.OrdinalIgnoreCase)
            && requestPort == authority.Port;
    }
}
