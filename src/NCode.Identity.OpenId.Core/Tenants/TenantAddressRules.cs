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
// See ADR-0159 (a tenant is named by its creator) and ADR-0160 (a tenant's address is validated wherever it is written).
internal static partial class TenantAddressRules
{
    /// <summary>
    /// Contains the requirement a tenant identifier must meet, phrased to complete a sentence such as "must be …".
    /// </summary>
    public const string TenantIdRequirement =
        "1 to 63 lowercase letters, digits, or hyphens that starts and ends with a letter or digit";

    /// <summary>
    /// Gets the requirement an issuer must meet, phrased to complete a sentence such as "must be …".
    /// </summary>
    public static string IssuerRequirement { get; } =
        "an absolute https URL (http only for a loopback host) with no user information, query, fragment, or "
        + $"whitespace, of at most {OpenIdMaxLengths.Issuer} characters";

    // An RFC 1123 host label in lowercase, so a tenant's name is equally valid as a URL path segment and as the leftmost
    // label of a hostname. '\z' rather than '$', which also matches before a trailing newline.
    [GeneratedRegex(@"^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\z", RegexOptions.CultureInvariant)]
    private static partial Regex TenantIdRegex();

    /// <summary>
    /// Determines whether the specified value is a valid tenant identifier: <see cref="TenantIdRequirement"/>.
    /// </summary>
    /// <param name="tenantId">The tenant identifier to check.</param>
    /// <returns><see langword="true"/> when the value is a valid tenant identifier; otherwise, <see langword="false"/>.</returns>
    public static bool IsValidTenantId([NotNullWhen(true)] string? tenantId) =>
        !string.IsNullOrEmpty(tenantId) && TenantIdRegex().IsMatch(tenantId);

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
            || !Uri.TryCreate(issuer, UriKind.Absolute, out var uri)
        )
        {
            return false;
        }

        var isAllowedScheme =
            uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback);
        if (!isAllowedScheme || !string.IsNullOrEmpty(uri.UserInfo))
            return false;

        issuerUri = uri;
        return true;
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
