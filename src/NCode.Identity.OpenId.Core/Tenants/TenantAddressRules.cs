// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Patterns;
using NCode.Identity.OpenId.Persistence;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Contains the rules for how a tenant is addressed — its name, its issuer, and the route it is served under. The
/// configuration validated when the host starts and the tenants written while it runs are judged by these same rules,
/// so the two cannot disagree about what a servable tenant is.
/// </summary>
// See ADR-1160 (a tenant is named by its creator).
internal static partial class TenantAddressRules
{
    /// <summary>
    /// Contains the requirement a tenant identifier must meet, phrased to complete a sentence such as "must be …".
    /// </summary>
    public const string TenantIdRequirement =
        "1 to 63 lowercase letters, digits, or hyphens that starts and ends with a letter or digit, and is not api, "
        + "bff, health, openapi, scalar or setup";

    /// <summary>
    /// Gets the requirement an issuer must meet, phrased to complete a sentence such as "must be …".
    /// </summary>
    public static string IssuerRequirement { get; } =
        "an absolute https URL (http only for a loopback host) with no user information, query, fragment, or "
        + $"whitespace, of at most {OpenIdMaxLengths.Issuer} characters, whose host name labels are each 1 to 63 "
        + "characters";

    /// <summary>
    /// Gets the requirement an issuer template must meet, phrased to complete a sentence such as "must be …".
    /// </summary>
    public static string IssuerTemplateRequirement { get; } =
        $"an absolute URL that contains '{TenantResolutionOptions.TenantIdPlaceholder}' exactly once and no other "
        + "template expression, and whose expansion for a tenant identifier is a valid issuer";

    /// <summary>
    /// Gets the requirement a mutual-TLS endpoint-alias template must meet, phrased to complete a sentence such as
    /// "must be …".
    /// </summary>
    public static string MtlsEndpointAliasTemplateRequirement { get; } =
        $"an https origin that contains '{TenantResolutionOptions.TenantIdPlaceholder}' exactly once and no other "
        + $"template expression, and whose expansion for a tenant identifier is {MtlsEndpointAliasAuthorityRequirement}";

    /// <summary>
    /// Gets the tenant identifiers an issuer template is expanded with to prove it yields a valid issuer, and a distinct
    /// one per tenant. An expansion that is valid for these can still be too long for a long identifier — a template that
    /// embeds the identifier in a longer host label — which is reported when that tenant is written.
    /// </summary>
    public static IReadOnlyList<string> ProbeTenantIds { get; } = ["a", "b"];

    private const int MaxHostLength = 253;

    private const int MaxHostLabelLength = 63;

    // Top-level path segments the host maps, which a path-strategy tenant would be shadowed by: api (the management
    // API root group prefix), setup (SetupDefaults.PathPrefix), bff (BffDefaults.EndpointPrefix), health (the health
    // probes), openapi and scalar (the API reference).
    private static FrozenSet<string> ReservedTenantIds { get; } =
        new[] { "api", "setup", "bff", "health", "openapi", "scalar" }.ToFrozenSet(
            StringComparer.Ordinal
        );

    /// <summary>
    /// Determines whether the specified value is a valid tenant identifier: <see cref="TenantIdRequirement"/>.
    /// </summary>
    /// <param name="tenantId">The tenant identifier to check.</param>
    /// <returns><see langword="true"/> when the value is a valid tenant identifier; otherwise, <see langword="false"/>.</returns>
    public static bool IsValidTenantId([NotNullWhen(true)] string? tenantId) =>
        TenantIdentifierRules.IsValidTenantId(tenantId) && !ReservedTenantIds.Contains(tenantId);

    /// <summary>
    /// Parses the specified issuer when it is <see cref="IssuerRequirement"/>, the form OpenID Connect Discovery and
    /// RFC 8414 require of an issuer identifier.
    /// </summary>
    /// <param name="issuer">The issuer to parse.</param>
    /// <param name="issuerUri">When this method returns <see langword="true"/>, contains the parsed issuer.</param>
    /// <returns><see langword="true"/> when the issuer is valid; otherwise, <see langword="false"/>.</returns>
    public static bool TryParseIssuer(
        [NotNullWhen(true)] string? issuer,
        [NotNullWhen(true)] out Uri? issuerUri
    )
    {
        issuerUri = null;

        // The raw text is checked as well as the parsed URI: the parser trims whitespace and drops an empty query or
        // fragment, so a value it accepts can still differ from the issuer clients must match byte for byte.
        if (
            string.IsNullOrEmpty(issuer)
            || issuer.Length > OpenIdMaxLengths.Issuer
            || issuer.AsSpan().IndexOfAny('?', '#') >= 0
            || issuer.Any(char.IsWhiteSpace)
            || !AbsoluteUri.TryParse(issuer, out var uri)
        )
        {
            return false;
        }

        var isAllowedScheme =
            uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback);
        if (!isAllowedScheme || !string.IsNullOrEmpty(uri.UserInfo) || !IsValidHost(uri))
            return false;

        issuerUri = uri;
        return true;
    }

    /// <summary>
    /// Describes the form a tenant's mutual-TLS endpoint-alias authority must have.
    /// </summary>
    public const string MtlsEndpointAliasAuthorityRequirement =
        "an https origin such as 'https://acme.mtls.example' — a scheme and authority with no path, query, fragment, or "
        + "user information";

    /// <summary>
    /// Parses the specified mutual-TLS endpoint-alias authority (RFC 8705 §5): an <c>https</c> origin — a scheme and
    /// authority with no path, query, fragment, or user information — whose host is a valid DNS host. Unlike an issuer, a
    /// loopback <c>http</c> origin is not accepted: the alias exists only to request a client certificate over TLS.
    /// </summary>
    /// <param name="authority">The authority to parse.</param>
    /// <param name="authorityUri">When this method returns <see langword="true"/>, contains the parsed authority.</param>
    /// <returns><see langword="true"/> when the authority is valid; otherwise, <see langword="false"/>.</returns>
    public static bool TryParseMtlsEndpointAliasAuthority(
        [NotNullWhen(true)] string? authority,
        [NotNullWhen(true)] out Uri? authorityUri
    )
    {
        authorityUri = null;

        if (
            !TryParseIssuer(authority, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !string.Equals(
                uri.GetLeftPart(UriPartial.Authority),
                authority.TrimEnd('/'),
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return false;
        }

        authorityUri = uri;
        return true;
    }

    // A host name whose labels exceed 63 characters, or whose whole exceeds 253, parses as a URI (the parser reports it as
    // a basic rather than a DNS host) but cannot be resolved or certified — the shape a template produces when it embeds
    // a long tenant identifier in a longer label.
    private static bool IsValidHost(Uri uri)
    {
        if (uri.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6)
            return true;

        var host = uri.IdnHost;
        if (host.Length > MaxHostLength)
            return false;

        foreach (var label in host.Split('.'))
        {
            if (label.Length is 0 or > MaxHostLabelLength)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Expands the specified issuer template for the specified tenant. A tenant identifier is a lowercase host label, so
    /// it needs no escaping in either the host or the path of a URL.
    /// </summary>
    /// <param name="issuerTemplate">The issuer template, carrying <see cref="TenantResolutionOptions.TenantIdPlaceholder"/>.</param>
    /// <param name="tenantId">The identifier of the tenant.</param>
    /// <returns>The issuer for the tenant.</returns>
    public static string ExpandIssuerTemplate(string issuerTemplate, string tenantId) =>
        issuerTemplate.Replace(
            TenantResolutionOptions.TenantIdPlaceholder,
            tenantId,
            StringComparison.Ordinal
        );

    /// <summary>
    /// Validates that the specified configured issuer template is <see cref="IssuerTemplateRequirement"/>, recording a
    /// failure under the specified configuration key when it is not. The rules of the strategy that uses the template
    /// are checked separately, against its expansions.
    /// </summary>
    /// <param name="issuerTemplate">The configured issuer template.</param>
    /// <param name="key">The configuration key the template was bound from, used in the failure message.</param>
    /// <param name="failures">The failures to append to.</param>
    /// <returns><see langword="true"/> when the template is valid; otherwise, <see langword="false"/>.</returns>
    public static bool TryValidateIssuerTemplate(
        string issuerTemplate,
        string key,
        List<string> failures
    )
    {
        if (
            IsSinglePlaceholderTemplate(issuerTemplate)
            && ProbeTenantIds.All(tenantId =>
                TryParseIssuer(ExpandIssuerTemplate(issuerTemplate, tenantId), out _)
            )
        )
        {
            return true;
        }

        failures.Add($"{key} '{issuerTemplate}' must be {IssuerTemplateRequirement}.");
        return false;
    }

    /// <summary>
    /// Validates that the specified configured mutual-TLS endpoint-alias template is
    /// <see cref="MtlsEndpointAliasTemplateRequirement"/>, recording a failure under the specified configuration key when
    /// it is not. The rules of the strategy that uses the template are checked separately, against its expansions.
    /// </summary>
    /// <param name="aliasTemplate">The configured alias template.</param>
    /// <param name="key">The configuration key the template was bound from, used in the failure message.</param>
    /// <param name="failures">The failures to append to.</param>
    /// <returns><see langword="true"/> when the template is valid; otherwise, <see langword="false"/>.</returns>
    public static bool TryValidateMtlsEndpointAliasTemplate(
        string aliasTemplate,
        string key,
        List<string> failures
    )
    {
        if (
            IsSinglePlaceholderTemplate(aliasTemplate)
            && ProbeTenantIds.All(tenantId =>
                TryParseMtlsEndpointAliasAuthority(
                    ExpandIssuerTemplate(aliasTemplate, tenantId),
                    out _
                )
            )
        )
        {
            return true;
        }

        failures.Add($"{key} '{aliasTemplate}' must be {MtlsEndpointAliasTemplateRequirement}.");
        return false;
    }

    private static bool IsSinglePlaceholderTemplate(string template)
    {
        const string placeholder = TenantResolutionOptions.TenantIdPlaceholder;

        var first = template.IndexOf(placeholder, StringComparison.Ordinal);
        var isSinglePlaceholder =
            first >= 0
            && template.IndexOf(placeholder, first + placeholder.Length, StringComparison.Ordinal)
                < 0;
        var remainder = template.Replace(placeholder, string.Empty, StringComparison.Ordinal);
        return isSinglePlaceholder && remainder.AsSpan().IndexOfAny('{', '}') < 0;
    }

    /// <summary>
    /// Maps the specified request host to the issuer host it routes to under the host strategy: the part of the host the
    /// configured pattern matches, or the whole host when the pattern does not match.
    /// </summary>
    /// <param name="hostPattern">The configured host pattern.</param>
    /// <param name="host">The request host.</param>
    /// <returns>The issuer host the request routes to.</returns>
    public static string MapToIssuerHost(Regex hostPattern, string host)
    {
        var match = hostPattern.Match(host);
        return match.Success ? match.Value : host;
    }

    /// <summary>
    /// Determines whether the path of the specified issuer ends with the specified route path — the path a tenant's
    /// endpoints and discovery document are served under. OpenID Connect Discovery requires the issuer to be the URL
    /// that document is fetched beneath, and any path base the host is mounted under precedes the route, so the issuer
    /// has to end with the route rather than equal it.
    /// </summary>
    /// <param name="issuer">The issuer to check.</param>
    /// <param name="routePath">The route path, with a leading slash, or empty when the tenant is served at the root.</param>
    /// <returns><see langword="true"/> when the issuer ends with the route path; otherwise, <see langword="false"/>.</returns>
    public static bool IssuerEndsWithRoutePath(Uri issuer, string? routePath)
    {
        var issuerPath = PathString.FromUriComponent(issuer).Value?.TrimEnd('/') ?? string.Empty;
        var tenantPath = routePath?.TrimEnd('/') ?? string.Empty;
        return issuerPath.EndsWith(tenantPath, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parses the specified configured route, recording a failure under the specified configuration key when it is
    /// not a valid route pattern.
    /// </summary>
    /// <param name="path">The configured route.</param>
    /// <param name="key">The configuration key the route was bound from, used in the failure message.</param>
    /// <param name="failures">The failures to append to.</param>
    /// <param name="route">When this method returns <see langword="true"/>, contains the parsed route.</param>
    /// <returns><see langword="true"/> when the route parsed; otherwise, <see langword="false"/>.</returns>
    public static bool TryParseRoute(
        PathString path,
        string key,
        List<string> failures,
        [NotNullWhen(true)] out RoutePattern? route
    )
    {
        try
        {
            route = RoutePatternFactory.Parse(path.Value ?? string.Empty);
            return true;
        }
        catch (RoutePatternException exception)
        {
            failures.Add($"{key} '{path}' is not a valid route pattern: {exception.Message}");
            route = null;
            return false;
        }
    }

    /// <summary>
    /// Validates that the specified configured route is a literal path, for a strategy that does not take the tenant
    /// from the route.
    /// </summary>
    /// <param name="path">The configured route.</param>
    /// <param name="key">The configuration key the route was bound from, used in the failure message.</param>
    /// <param name="failures">The failures to append to.</param>
    /// <returns><see langword="true"/> when the route is a literal path; otherwise, <see langword="false"/>.</returns>
    public static bool TryParseLiteralPath(PathString path, string key, List<string> failures)
    {
        if (!TryParseRoute(path, key, failures, out var route))
            return false;

        if (route.Parameters.Count == 0)
            return true;

        failures.Add(
            $"{key} '{path}' must be a literal path: this strategy does not take the tenant from the route."
        );
        return false;
    }
}
