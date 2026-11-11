// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Identity.Time;

namespace NCode.Identity.Bff.Blazor.Client.Options;

/// <summary>
/// Validates <see cref="BffBlazorClientOptions"/>, so a setting that cannot work stops the client instead of failing
/// on the first request.
/// </summary>
internal sealed class BffBlazorClientOptionsValidator : IValidateOptions<BffBlazorClientOptions>
{
    // The server's BffOptionsValidator refuses the same names for Bff:Antiforgery:HeaderName.
    private static string[] ForbiddenHeaderNames { get; } =
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

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, BffBlazorClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];

        var userPath = options.UserPath;
        if (
            string.IsNullOrEmpty(userPath)
            || userPath[0] != '/'
            || (userPath.Length > 1 && userPath[1] == '/')
        )
        {
            failures.Add(
                $"BffBlazorClientOptions.UserPath '{userPath}' must start with one '/' and not '//'."
            );
        }

        var headerName = options.AntiforgeryHeaderName;
        if (string.IsNullOrWhiteSpace(headerName))
        {
            failures.Add("BffBlazorClientOptions.AntiforgeryHeaderName must not be blank.");
        }
        else if (
            ForbiddenHeaderNames.Any(x =>
                string.Equals(x, headerName, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            failures.Add(
                $"BffBlazorClientOptions.AntiforgeryHeaderName '{headerName}' must not be a header that skips a CORS preflight or carries credentials."
            );
        }

        if (string.IsNullOrWhiteSpace(options.AntiforgeryHeaderValue))
        {
            failures.Add("BffBlazorClientOptions.AntiforgeryHeaderValue must not be blank.");
        }

        if (!TimerPeriod.IsValid(options.ProbeInterval))
        {
            failures.Add(
                $"BffBlazorClientOptions.ProbeInterval '{options.ProbeInterval}' must be a valid timer period."
            );
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
