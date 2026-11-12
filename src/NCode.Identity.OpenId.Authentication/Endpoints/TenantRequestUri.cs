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
/// (RFC 8705 §5) — and only the path comes from the request, which routing already matched to this endpoint, as the
/// discovery document builds the URLs it publishes. The request's <c>Host</c> therefore never widens the values the
/// server accepts: a forged <c>Host</c> chooses at most which of the tenant's own published origins is compared.
/// </remarks>
// Mirrors OpenIdLinkGeneratorExtensions.GetTenantUriByName. See project-template ADR-0126 and ADR-1365.
internal static class TenantRequestUri
{
    private const int DefaultHttpsPort = 443;

    private const int DefaultHttpPort = 80;

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

    /// <summary>
    /// Gets the URL of the endpoint receiving the request under the tenant's base address: the issuer's scheme and
    /// authority, followed by the request's path base and path.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> for the current request.</param>
    /// <returns>The endpoint's URL under the tenant's base address.</returns>
    private static string GetBaseAddressUri(OpenIdContext openIdContext)
    {
        var request = openIdContext.Http.Request;
        var baseAddress = openIdContext.Tenant.BaseAddress;
        return $"{baseAddress.Scheme}://{baseAddress.Host}{request.PathBase}{request.Path}";
    }

    /// <summary>
    /// Gets the URL of the endpoint receiving the request under the tenant's mutual-TLS endpoint-alias authority,
    /// together with that authority, or <see langword="null"/> when the tenant publishes no alias.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> for the current request.</param>
    /// <returns>The endpoint's URL under the alias and the alias authority, or <see langword="null"/>.</returns>
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

    /// <summary>
    /// Determines whether the request was sent to the specified authority: the same host, compared without case, and
    /// the same port, where a request without a port uses its scheme's default port.
    /// </summary>
    /// <param name="request">The current request.</param>
    /// <param name="authority">The authority to compare the request's <c>Host</c> with.</param>
    /// <returns><see langword="true"/> when the request was sent to the authority; otherwise, <see langword="false"/>.</returns>
    private static bool IsSentTo(HttpRequest request, Uri authority)
    {
        var host = request.Host;
        if (!host.HasValue)
            return false;

        // The authority's IdnHost is in punycode, and HostString encodes a Unicode host the same way.
        var requestHost = new HostString(host.Host).ToUriComponent();
        var requestPort = host.Port ?? (request.IsHttps ? DefaultHttpsPort : DefaultHttpPort);
        return string.Equals(requestHost, authority.IdnHost, StringComparison.OrdinalIgnoreCase)
            && requestPort == authority.Port;
    }
}
