// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.Bff.Navigation;
using NCode.Identity.Bff.RemoteApis;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.OpenId;
using NCode.Time;

namespace NCode.Identity.Bff.Options;

/// <summary>
/// Validates the settings of one <see cref="BffFrontendOptions"/>, naming each failure by the configuration key the
/// frontend is bound from.
/// </summary>
internal static class BffFrontendOptionsValidation
{
    private static string[] ReservedUserClaims { get; } =
    ["iss", "sid", "nonce", "at_hash", "c_hash", "s_hash", "aud", "azp", "exp", "iat", "nbf"];

    private static string[] AllowedLoginParameters { get; } =
    [
        OpenIdProtocol.Parameters.Prompt,
        OpenIdProtocol.Parameters.MaxAge,
        OpenIdProtocol.Parameters.AcrValues,
        OpenIdProtocol.Parameters.LoginHint,
        OpenIdProtocol.Parameters.UiLocales,
    ];

    /// <summary>
    /// Adds a failure for every setting of <paramref name="options"/> that cannot work.
    /// </summary>
    internal static void Validate(
        string key,
        BffFrontendOptions? options,
        bool isDevelopment,
        List<string> failures
    )
    {
        if (options is null)
        {
            failures.Add($"{key} must be set.");
            return;
        }

        if (options.Selection is null)
        {
            failures.Add($"{key}:Selection must be set.");
        }

        ValidateAuthority(key, options.Authority, isDevelopment, failures);
        ValidatePublicOrigin(key, options.PublicOrigin, isDevelopment, failures);

        if (string.IsNullOrWhiteSpace(options.ClientId))
        {
            failures.Add($"{key}:ClientId must not be blank.");
        }

        if (string.IsNullOrWhiteSpace(options.ClientAuthentication?.Method))
        {
            failures.Add($"{key}:ClientAuthentication:Method must not be blank.");
        }

        ValidateReturnUrls(key, options.ReturnUrls, failures);

        if (!BffReturnUrls.IsWellFormedPath(options.ErrorPath ?? string.Empty))
        {
            failures.Add($"{key}:ErrorPath must be a well-formed local path that starts with '/'.");
        }

        ValidatePathPrefix(key, options.PathPrefix, failures);
        ValidateTrustedOrigins(key, options.TrustedOrigins, isDevelopment, failures);
        ValidateSession(key, options.Session, failures);
        ValidateDPoP(key, options.DPoP, failures);
        ValidateIdToken(key, options.IdToken, options.ClientAuthentication, failures);
        ValidateUserInfo(key, options.UserInfo, options.ClientAuthentication, failures);
        ValidateSecurityHeaders(key, options.SecurityHeaders, failures);
        ValidateJwks(key, options.Jwks, options.ClientAuthentication, failures);
        ValidateUserClaims(key, options.UserClaims, failures);
        ValidateLoginParameters(key, options.LoginParameters, failures);
        ValidateRemoteApis(key, options.RemoteApis, options.PathPrefix, isDevelopment, failures);
    }

    internal static void ValidateSession(
        string key,
        BffSessionOptions? session,
        List<string> failures
    )
    {
        if (session is null)
        {
            failures.Add($"{key}:Session must be set.");
            return;
        }

        if (
            !Enum.IsDefined(session.SameSite)
            || session.SameSite is not (SameSiteMode.Strict or SameSiteMode.Lax)
        )
        {
            failures.Add($"{key}:Session:SameSite must be 'Strict' or 'Lax'.");
        }

        if (session.IdleTimeout <= TimeSpan.Zero)
        {
            failures.Add($"{key}:Session:IdleTimeout must be greater than zero.");
        }

        if (session.AbsoluteLifetime < session.IdleTimeout)
        {
            failures.Add(
                $"{key}:Session:AbsoluteLifetime must be greater than or equal to {key}:Session:IdleTimeout."
            );
        }

        if (!TimerPeriod.IsValid(session.RevalidationInterval))
        {
            failures.Add($"{key}:Session:RevalidationInterval must be a valid timer period.");
        }

        if (session.RevalidationInterval > session.IdleTimeout / 2)
        {
            failures.Add(
                $"{key}:Session:RevalidationInterval must be at most half of {key}:Session:IdleTimeout."
            );
        }

        if (
            session.LoginLifetime < TimeSpan.FromMinutes(1)
            || session.LoginLifetime > TimeSpan.FromMinutes(30)
        )
        {
            failures.Add($"{key}:Session:LoginLifetime must be between 1 and 30 minutes.");
        }
    }

    internal static void ValidateDPoP(string key, BffDPoPOptions? dpop, List<string> failures)
    {
        if (dpop is null)
        {
            failures.Add($"{key}:DPoP must be set.");
            return;
        }

        var isRequired = string.Equals(dpop.Mode, BffDPoPModes.Required, StringComparison.Ordinal);
        var isKnown =
            isRequired
            || string.Equals(dpop.Mode, BffDPoPModes.WhenSupported, StringComparison.Ordinal);

        if (!isKnown)
        {
            failures.Add(
                $"{key}:DPoP:Mode must be '{BffDPoPModes.WhenSupported}' or '{BffDPoPModes.Required}'."
            );
        }
        else if (isRequired && string.IsNullOrWhiteSpace(dpop.KeyRing))
        {
            failures.Add(
                $"{key}:DPoP:KeyRing must not be blank when {key}:DPoP:Mode is 'required'."
            );
        }
    }

    // See ADR-1301.
    internal static void ValidateIdToken(
        string key,
        BffIdTokenOptions? idToken,
        BffClientAuthenticationOptions? clientAuthentication,
        List<string> failures
    )
    {
        if (idToken is null)
        {
            failures.Add($"{key}:IdToken must be set.");
            return;
        }

        ValidateResponseProtection(
            key,
            "IdToken",
            idToken.SigningAlgorithms,
            idToken.KeyManagementAlgorithms,
            idToken.ContentEncryptionAlgorithms,
            clientAuthentication,
            failures
        );
    }

    // See ADR-1301.
    internal static void ValidateUserInfo(
        string key,
        BffUserInfoOptions? userInfo,
        BffClientAuthenticationOptions? clientAuthentication,
        List<string> failures
    )
    {
        if (userInfo is null)
        {
            failures.Add($"{key}:UserInfo must be set.");
            return;
        }

        ValidateResponseProtection(
            key,
            "UserInfo",
            userInfo.SigningAlgorithms,
            userInfo.KeyManagementAlgorithms,
            userInfo.ContentEncryptionAlgorithms,
            clientAuthentication,
            failures
        );

        // A JWE that encloses no signature is refused, so UserInfo encryption is usable only with UserInfo signing.
        if (
            userInfo.KeyManagementAlgorithms is { Count: > 0 }
            && userInfo.SigningAlgorithms is not { Count: > 0 }
        )
        {
            failures.Add(
                $"{key}:UserInfo:KeyManagementAlgorithms requires {key}:UserInfo:SigningAlgorithms, because an encrypted UserInfo response must also be signed."
            );
        }
    }

    internal static void ValidateResponseProtection(
        string key,
        string section,
        List<string>? signingAlgorithms,
        List<string>? keyManagementAlgorithms,
        List<string>? contentEncryptionAlgorithms,
        BffClientAuthenticationOptions? clientAuthentication,
        List<string> failures
    )
    {
        section = $"{key}:{section}";

        ValidateAlgorithmList($"{section}:SigningAlgorithms", signingAlgorithms, failures);
        ValidateAlgorithmList(
            $"{section}:KeyManagementAlgorithms",
            keyManagementAlgorithms,
            failures
        );
        ValidateAlgorithmList(
            $"{section}:ContentEncryptionAlgorithms",
            contentEncryptionAlgorithms,
            failures
        );

        var encrypted = keyManagementAlgorithms is { Count: > 0 };
        if (!encrypted && contentEncryptionAlgorithms is { Count: > 0 })
        {
            failures.Add(
                $"{section}:ContentEncryptionAlgorithms requires {section}:KeyManagementAlgorithms."
            );
        }

        if (encrypted && string.IsNullOrWhiteSpace(clientAuthentication?.KeyRing))
        {
            failures.Add(
                $"{key}:ClientAuthentication:KeyRing must not be blank when {section}:KeyManagementAlgorithms is set, because it holds the decryption keys."
            );
        }
    }

    internal static void ValidateAlgorithmList(
        string key,
        List<string>? algorithms,
        List<string> failures
    )
    {
        if (algorithms is null)
        {
            failures.Add($"{key} must be set.");
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var algorithm in algorithms)
        {
            if (string.IsNullOrWhiteSpace(algorithm))
            {
                failures.Add($"{key} must not contain a blank entry.");
            }
            else if (
                string.Equals(
                    algorithm,
                    AlgorithmCodes.DigitalSignature.None,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                failures.Add($"{key} must not contain 'none'.");
            }
            else if (!seen.Add(algorithm))
            {
                failures.Add($"{key} must not contain '{algorithm}' more than once.");
            }
        }
    }

    internal static void ValidateSecurityHeaders(
        string key,
        BffSecurityHeadersOptions? securityHeaders,
        List<string> failures
    )
    {
        if (securityHeaders is null)
        {
            failures.Add($"{key}:SecurityHeaders must be set.");
            return;
        }

        ValidateHeaderValue(
            securityHeaders.ContentSecurityPolicy,
            $"{key}:SecurityHeaders:ContentSecurityPolicy",
            failures
        );
        ValidateHeaderValue(
            securityHeaders.ReferrerPolicy,
            $"{key}:SecurityHeaders:ReferrerPolicy",
            failures
        );
        ValidateHeaderValue(
            securityHeaders.CrossOriginOpenerPolicy,
            $"{key}:SecurityHeaders:CrossOriginOpenerPolicy",
            failures
        );
    }

    internal static void ValidateJwks(
        string key,
        BffJwksOptions? jwks,
        BffClientAuthenticationOptions? clientAuthentication,
        List<string> failures
    )
    {
        if (jwks is null)
        {
            failures.Add($"{key}:Jwks must be set.");
        }
        else if (jwks.Enabled && string.IsNullOrWhiteSpace(clientAuthentication?.KeyRing))
        {
            failures.Add(
                $"{key}:ClientAuthentication:KeyRing must not be blank when {key}:Jwks:Enabled is true, because it holds the keys the endpoint publishes."
            );
        }
    }

    internal static void ValidateHeaderValue(string? value, string key, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            failures.Add($"{key} must not be blank.");
        }
        else if (value.Any(x => x is < ' ' or > '~'))
        {
            failures.Add($"{key} must hold only printable ASCII characters.");
        }
    }

    internal static void ValidateUserClaims(
        string key,
        List<string>? userClaims,
        List<string> failures
    )
    {
        foreach (var claim in userClaims ?? [])
        {
            if (ReservedUserClaims.Contains(claim, StringComparer.Ordinal))
            {
                failures.Add($"{key}:UserClaims entry '{claim}' is reserved and cannot be listed.");
            }
        }
    }

    internal static void ValidateLoginParameters(
        string key,
        List<string>? loginParameters,
        List<string> failures
    )
    {
        foreach (var parameter in loginParameters ?? [])
        {
            if (!AllowedLoginParameters.Contains(parameter, StringComparer.Ordinal))
            {
                failures.Add(
                    $"{key}:LoginParameters entry '{parameter}' is not allowed; use only prompt, max_age, acr_values, login_hint and ui_locales."
                );
            }
        }
    }

    internal static void ValidateRemoteApis(
        string key,
        Dictionary<string, BffRemoteApiOptions>? remoteApis,
        string? pathPrefix,
        bool isDevelopment,
        List<string> failures
    )
    {
        if (remoteApis is null)
        {
            failures.Add($"{key}:RemoteApis must be set.");
            return;
        }

        List<(string Name, string Path)> paths = [];
        foreach (var (name, api) in remoteApis)
        {
            var apiKey = $"{key}:RemoteApis:{name}";
            BffRemoteApiValidation.Validate(apiKey, api, isDevelopment, failures);
            if (string.IsNullOrEmpty(api?.Path))
                continue;

            if (
                !string.IsNullOrEmpty(pathPrefix)
                && BffRemoteApiValidation.Overlaps(api.Path, pathPrefix)
            )
            {
                failures.Add(
                    $"{apiKey}:Path '{api.Path}' must not overlap {key}:PathPrefix '{pathPrefix}'."
                );
            }

            foreach (var (otherName, otherPath) in paths)
            {
                if (BffRemoteApiValidation.Overlaps(api.Path, otherPath))
                {
                    failures.Add(
                        $"{apiKey}:Path '{api.Path}' must not overlap {key}:RemoteApis:{otherName}:Path '{otherPath}'."
                    );
                }
            }

            paths.Add((name, api.Path));
        }
    }

    // A URL with user info may carry a password, which a failure message never quotes.
    internal static string QuoteUrl(string? url) =>
        url is not null && url.Contains('@', StringComparison.Ordinal)
            ? "(withheld because it contains '@')"
            : $"'{url}'";

    internal static void ValidateAuthority(
        string key,
        string? authority,
        bool isDevelopment,
        List<string> failures
    )
    {
        const string https = "https:";
        const string http = "http:";

        var isValid =
            !string.IsNullOrEmpty(authority)
            && !authority.Any(char.IsWhiteSpace)
            && AbsoluteUri.TryParse(authority, out _)
            && (
                authority.StartsWith(https, StringComparison.OrdinalIgnoreCase)
                || (isDevelopment && authority.StartsWith(http, StringComparison.OrdinalIgnoreCase))
            );

        if (!isValid)
        {
            failures.Add(
                isDevelopment
                    ? $"{key}:Authority {QuoteUrl(authority)} must be an absolute 'https:' or 'http:' URI without whitespace."
                    : $"{key}:Authority {QuoteUrl(authority)} must be an absolute 'https:' URI without whitespace."
            );
        }
    }

    internal static void ValidatePublicOrigin(
        string key,
        string? publicOrigin,
        bool isDevelopment,
        List<string> failures
    )
    {
        var isValid =
            !string.IsNullOrEmpty(publicOrigin)
            && AbsoluteUri.TryParse(publicOrigin, out var uri)
            && uri.UserInfo.Length == 0
            && string.Equals(
                publicOrigin,
                uri.GetLeftPart(UriPartial.Authority) + uri.AbsolutePath.TrimEnd('/'),
                StringComparison.Ordinal
            )
            && (
                string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
                || (
                    isDevelopment
                    && string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)
                )
            );

        if (!isValid)
        {
            failures.Add(
                $"{key}:PublicOrigin {QuoteUrl(publicOrigin)} must be the absolute URL the BFF is published at — scheme, host, optional port and path base, without user info, a query, a fragment or a trailing '/' — and must use 'https' outside Development."
            );
        }
    }

    internal static void ValidateReturnUrls(
        string key,
        List<string>? returnUrls,
        List<string> failures
    )
    {
        if (returnUrls is null || returnUrls.Count == 0)
        {
            failures.Add($"{key}:ReturnUrls must contain at least one local path.");
            return;
        }

        foreach (var entry in returnUrls)
        {
            if (
                entry is null
                || !BffReturnUrls.IsWellFormedPath(entry)
                || entry.Contains('?', StringComparison.Ordinal)
            )
            {
                failures.Add(
                    $"{key}:ReturnUrls entry '{entry}' must be a well-formed local path that starts with '/' and has no query."
                );
            }
        }
    }

    internal static void ValidatePathPrefix(string key, string? pathPrefix, List<string> failures)
    {
        var isValid =
            !string.IsNullOrEmpty(pathPrefix)
            && pathPrefix[0] == '/'
            && pathPrefix[^1] != '/'
            && BffReturnUrls.IsWellFormedPath(pathPrefix);

        if (!isValid)
        {
            failures.Add(
                $"{key}:PathPrefix must be a well-formed local path that starts with '/' and does not end with '/'."
            );
        }
    }

    internal static void ValidateTrustedOrigins(
        string key,
        List<string>? trustedOrigins,
        bool isDevelopment,
        List<string> failures
    )
    {
        foreach (var entry in trustedOrigins ?? [])
        {
            var isValid =
                !string.IsNullOrEmpty(entry)
                && AbsoluteUri.TryParse(entry, out var uri)
                && string.Equals(
                    entry,
                    uri.GetLeftPart(UriPartial.Authority),
                    StringComparison.Ordinal
                )
                && (
                    isDevelopment
                    || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
                );

            if (!isValid)
            {
                failures.Add(
                    $"{key}:TrustedOrigins entry '{entry}' must be an origin (scheme, host and optional port) without a path or trailing '/', and must use 'https' outside Development."
                );
            }
        }
    }
}
