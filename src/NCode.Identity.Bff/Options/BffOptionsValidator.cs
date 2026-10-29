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

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NCode.Identity.Bff.Navigation;
using NCode.Identity.OpenId;

namespace NCode.Identity.Bff.Options;

/// <summary>
/// Validates <see cref="BffOptions"/> when the host starts, so a setting that cannot work stops the host instead of
/// failing on the first request.
/// </summary>
internal class BffOptionsValidator(IHostEnvironment environment) : IValidateOptions<BffOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, BffOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];
        var isDevelopment = environment.IsDevelopment();

        ValidateAuthority(options.Authority, isDevelopment, failures);

        if (string.IsNullOrWhiteSpace(options.ClientId))
        {
            failures.Add("Bff:ClientId must not be blank.");
        }

        if (string.IsNullOrWhiteSpace(options.ClientAuthentication?.Method))
        {
            failures.Add("Bff:ClientAuthentication:Method must not be blank.");
        }

        ValidateReturnUrls(options.ReturnUrls, failures);

        if (!BffReturnUrls.IsWellFormedPath(options.ErrorPath ?? string.Empty))
        {
            failures.Add("Bff:ErrorPath must be a well-formed local path that starts with '/'.");
        }

        ValidatePathPrefix(options.PathPrefix, failures);
        ValidateTrustedOrigins(options.TrustedOrigins, isDevelopment, failures);
        ValidateSession(options.Session, failures);
        ValidateDPoP(options.DPoP, failures);
        ValidateTokens(options.Tokens, failures);
        ValidateAntiforgery(options.Antiforgery, failures);
        ValidateUserClaims(options.UserClaims, failures);
        ValidateLoginParameters(options.LoginParameters, failures);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static readonly string[] ForbiddenHeaderNames =
    [
        "Accept",
        "Accept-Language",
        "Content-Language",
        "Content-Type",
        "Range",
        "Cookie",
        "Authorization",
        "DPoP",
    ];

    private static readonly string[] ReservedUserClaims =
    [
        "iss",
        "sid",
        "nonce",
        "at_hash",
        "c_hash",
        "s_hash",
        "aud",
        "azp",
        "exp",
        "iat",
        "nbf",
    ];

    private static readonly string[] AllowedLoginParameters =
    [
        OpenIdProtocol.Parameters.Prompt,
        OpenIdProtocol.Parameters.MaxAge,
        OpenIdProtocol.Parameters.AcrValues,
        OpenIdProtocol.Parameters.LoginHint,
        OpenIdProtocol.Parameters.UiLocales,
    ];

    private static void ValidateSession(BffSessionOptions? session, List<string> failures)
    {
        if (session is null)
        {
            failures.Add("Bff:Session must be set.");
            return;
        }

        if (
            !Enum.IsDefined(session.SameSite)
            || session.SameSite is not (SameSiteMode.Strict or SameSiteMode.Lax)
        )
        {
            failures.Add("Bff:Session:SameSite must be 'Strict' or 'Lax'.");
        }

        if (session.IdleTimeout <= TimeSpan.Zero)
        {
            failures.Add("Bff:Session:IdleTimeout must be greater than zero.");
        }

        if (session.AbsoluteLifetime < session.IdleTimeout)
        {
            failures.Add(
                "Bff:Session:AbsoluteLifetime must be greater than or equal to Bff:Session:IdleTimeout."
            );
        }

        if (
            session.LoginLifetime < TimeSpan.FromMinutes(1)
            || session.LoginLifetime > TimeSpan.FromMinutes(30)
        )
        {
            failures.Add("Bff:Session:LoginLifetime must be between 1 and 30 minutes.");
        }
    }

    private static void ValidateDPoP(BffDPoPOptions? dpop, List<string> failures)
    {
        if (dpop is null)
        {
            failures.Add("Bff:DPoP must be set.");
            return;
        }

        var isRequired = string.Equals(dpop.Mode, BffDPoPModes.Required, StringComparison.Ordinal);
        var isKnown =
            isRequired
            || string.Equals(dpop.Mode, BffDPoPModes.WhenSupported, StringComparison.Ordinal);

        if (!isKnown)
        {
            failures.Add(
                $"Bff:DPoP:Mode must be '{BffDPoPModes.WhenSupported}' or '{BffDPoPModes.Required}'."
            );
        }
        else if (isRequired && string.IsNullOrWhiteSpace(dpop.KeyRing))
        {
            failures.Add("Bff:DPoP:KeyRing must not be blank when Bff:DPoP:Mode is 'required'.");
        }
    }

    private static void ValidateTokens(BffTokenOptions? tokens, List<string> failures)
    {
        if (tokens is null)
        {
            failures.Add("Bff:Tokens must be set.");
            return;
        }

        if (
            tokens.RefreshBeforeExpiry < TimeSpan.Zero
            || tokens.RefreshBeforeExpiry > TimeSpan.FromHours(1)
        )
        {
            failures.Add("Bff:Tokens:RefreshBeforeExpiry must be between 0 and 1 hour.");
        }

        if (
            tokens.RefreshLeaseDuration < TimeSpan.FromSeconds(10)
            || tokens.RefreshLeaseDuration > TimeSpan.FromMinutes(5)
        )
        {
            failures.Add(
                "Bff:Tokens:RefreshLeaseDuration must be between 10 seconds and 5 minutes."
            );
        }
    }

    private static void ValidateAntiforgery(
        BffAntiforgeryOptions? antiforgery,
        List<string> failures
    )
    {
        if (antiforgery is null)
        {
            failures.Add("Bff:Antiforgery must be set.");
            return;
        }

        if (string.IsNullOrWhiteSpace(antiforgery.HeaderName))
        {
            failures.Add("Bff:Antiforgery:HeaderName must not be blank.");
        }
        else if (
            ForbiddenHeaderNames.Any(x =>
                string.Equals(x, antiforgery.HeaderName, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            failures.Add(
                $"Bff:Antiforgery:HeaderName '{antiforgery.HeaderName}' must not be a header that skips a CORS preflight or carries credentials."
            );
        }

        if (string.IsNullOrWhiteSpace(antiforgery.HeaderValue))
        {
            failures.Add("Bff:Antiforgery:HeaderValue must not be blank.");
        }
    }

    private static void ValidateUserClaims(List<string>? userClaims, List<string> failures)
    {
        foreach (var claim in userClaims ?? [])
        {
            if (ReservedUserClaims.Contains(claim, StringComparer.Ordinal))
            {
                failures.Add($"Bff:UserClaims entry '{claim}' is reserved and cannot be listed.");
            }
        }
    }

    private static void ValidateLoginParameters(
        List<string>? loginParameters,
        List<string> failures
    )
    {
        foreach (var parameter in loginParameters ?? [])
        {
            if (!AllowedLoginParameters.Contains(parameter, StringComparer.Ordinal))
            {
                failures.Add(
                    $"Bff:LoginParameters entry '{parameter}' is not allowed; use only prompt, max_age, acr_values, login_hint and ui_locales."
                );
            }
        }
    }

    private static void ValidateAuthority(
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
            && Uri.TryCreate(authority, UriKind.Absolute, out _)
            && (
                authority.StartsWith(https, StringComparison.OrdinalIgnoreCase)
                || (isDevelopment && authority.StartsWith(http, StringComparison.OrdinalIgnoreCase))
            );

        if (!isValid)
        {
            failures.Add(
                isDevelopment
                    ? "Bff:Authority must be an absolute 'https:' or 'http:' URI without whitespace."
                    : "Bff:Authority must be an absolute 'https:' URI without whitespace."
            );
        }
    }

    private static void ValidateReturnUrls(List<string>? returnUrls, List<string> failures)
    {
        if (returnUrls is null || returnUrls.Count == 0)
        {
            failures.Add("Bff:ReturnUrls must contain at least one local path.");
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
                    $"Bff:ReturnUrls entry '{entry}' must be a well-formed local path that starts with '/' and has no query."
                );
            }
        }
    }

    private static void ValidatePathPrefix(string? pathPrefix, List<string> failures)
    {
        var isValid =
            !string.IsNullOrEmpty(pathPrefix)
            && pathPrefix[0] == '/'
            && pathPrefix[^1] != '/'
            && BffReturnUrls.IsWellFormedPath(pathPrefix);

        if (!isValid)
        {
            failures.Add(
                "Bff:PathPrefix must be a well-formed local path that starts with '/' and does not end with '/'."
            );
        }
    }

    private static void ValidateTrustedOrigins(
        List<string>? trustedOrigins,
        bool isDevelopment,
        List<string> failures
    )
    {
        foreach (var entry in trustedOrigins ?? [])
        {
            var isValid =
                !string.IsNullOrEmpty(entry)
                && Uri.TryCreate(entry, UriKind.Absolute, out var uri)
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
                    $"Bff:TrustedOrigins entry '{entry}' must be an origin (scheme, host and optional port) without a path or trailing '/', and must use 'https' outside Development."
                );
            }
        }
    }
}
