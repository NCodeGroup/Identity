// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NCode.Identity.Bff.Navigation;

namespace NCode.Identity.Bff.Options;

/// <summary>
/// Validates <see cref="BffOptions"/> when the host starts, so a setting that cannot work stops the host instead of
/// failing on the first request.
/// </summary>
internal class BffOptionsValidator(IHostEnvironment environment) : IValidateOptions<BffOptions>
{
    private const string Key = "Bff";

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, BffOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];
        var isDevelopment = environment.IsDevelopment();

        BffFrontendOptionsValidation.ValidateAuthority(
            Key,
            options.Authority,
            isDevelopment,
            failures
        );
        BffFrontendOptionsValidation.ValidatePublicOrigin(
            Key,
            options.PublicOrigin,
            isDevelopment,
            failures
        );

        if (string.IsNullOrWhiteSpace(options.ClientId))
        {
            failures.Add($"{Key}:ClientId must not be blank.");
        }

        if (string.IsNullOrWhiteSpace(options.ClientAuthentication?.Method))
        {
            failures.Add($"{Key}:ClientAuthentication:Method must not be blank.");
        }

        BffFrontendOptionsValidation.ValidateReturnUrls(Key, options.ReturnUrls, failures);

        if (!BffReturnUrls.IsWellFormedPath(options.ErrorPath ?? string.Empty))
        {
            failures.Add($"{Key}:ErrorPath must be a well-formed local path that starts with '/'.");
        }

        BffFrontendOptionsValidation.ValidatePathPrefix(Key, options.PathPrefix, failures);
        BffFrontendOptionsValidation.ValidateTrustedOrigins(
            Key,
            options.TrustedOrigins,
            isDevelopment,
            failures
        );
        BffFrontendOptionsValidation.ValidateSession(Key, options.Session, failures);
        BffFrontendOptionsValidation.ValidateDPoP(Key, options.DPoP, failures);
        BffFrontendOptionsValidation.ValidateIdToken(
            Key,
            options.IdToken,
            options.ClientAuthentication,
            failures
        );
        BffFrontendOptionsValidation.ValidateUserInfo(
            Key,
            options.UserInfo,
            options.ClientAuthentication,
            failures
        );
        ValidateTokens(options.Tokens, failures);
        ValidateAntiforgery(options.Antiforgery, failures);
        BffFrontendOptionsValidation.ValidateSecurityHeaders(
            Key,
            options.SecurityHeaders,
            failures
        );
        BffFrontendOptionsValidation.ValidateJwks(
            Key,
            options.Jwks,
            options.ClientAuthentication,
            failures
        );
        BffFrontendOptionsValidation.ValidateUserClaims(Key, options.UserClaims, failures);
        BffFrontendOptionsValidation.ValidateLoginParameters(
            Key,
            options.LoginParameters,
            failures
        );
        BffFrontendOptionsValidation.ValidateRemoteApis(
            Key,
            options.RemoteApis,
            options.PathPrefix,
            isDevelopment,
            failures
        );

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
}
