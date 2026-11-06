// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace NCode.Identity.Bff.Options;

/// <summary>
/// Validates the antiforgery cookie when the host starts, so a cookie that a sibling subdomain could set or read stops
/// the host instead of weakening the guard of a page's forms.
/// </summary>
internal sealed class BffAntiforgeryCookieValidator : IValidateOptions<AntiforgeryOptions>
{
    private const string HostPrefix = "__Host-";

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, AntiforgeryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];
        var cookie = options.Cookie;

        if (cookie.Name?.StartsWith(HostPrefix, StringComparison.Ordinal) != true)
            failures.Add(
                $"AntiforgeryOptions.Cookie.Name '{cookie.Name}' must start with '__Host-'."
            );

        if (cookie.SecurePolicy != CookieSecurePolicy.Always)
            failures.Add(
                $"AntiforgeryOptions.Cookie.SecurePolicy '{cookie.SecurePolicy}' must be Always."
            );

        if (cookie.Path != "/")
            failures.Add($"AntiforgeryOptions.Cookie.Path '{cookie.Path}' must be '/'.");

        if (!string.IsNullOrEmpty(cookie.Domain))
            failures.Add($"AntiforgeryOptions.Cookie.Domain '{cookie.Domain}' must not be set.");

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
