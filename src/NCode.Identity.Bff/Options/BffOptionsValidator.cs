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

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
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
