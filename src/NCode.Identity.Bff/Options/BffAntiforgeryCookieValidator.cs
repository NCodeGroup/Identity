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

    public ValidateOptionsResult Validate(string? name, AntiforgeryOptions options)
    {
        var failures = new List<string>();
        var cookie = options.Cookie;

        if (cookie.Name?.StartsWith(HostPrefix, StringComparison.Ordinal) != true)
            failures.Add("The antiforgery cookie's name must start with '__Host-'.");

        if (cookie.SecurePolicy != CookieSecurePolicy.Always)
            failures.Add("The antiforgery cookie must be Secure always.");

        if (cookie.Path != "/")
            failures.Add("The antiforgery cookie's path must be '/'.");

        if (!string.IsNullOrEmpty(cookie.Domain))
            failures.Add("The antiforgery cookie must not set a domain.");

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
