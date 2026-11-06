// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Time;

namespace NCode.Identity.Bff.Blazor.Client.Options;

/// <summary>
/// Validates <see cref="BffBlazorClientOptions"/>, so a setting that cannot work stops the client instead of failing
/// on the first request.
/// </summary>
internal sealed class BffBlazorClientOptionsValidator : IValidateOptions<BffBlazorClientOptions>
{
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
            failures.Add("UserPath must start with one '/' and not '//'.");
        }

        if (string.IsNullOrWhiteSpace(options.AntiforgeryHeaderName))
        {
            failures.Add("AntiforgeryHeaderName must not be blank.");
        }

        if (string.IsNullOrWhiteSpace(options.AntiforgeryHeaderValue))
        {
            failures.Add("AntiforgeryHeaderValue must not be blank.");
        }

        if (!TimerPeriod.IsValid(options.ProbeInterval))
        {
            failures.Add("ProbeInterval must be a valid timer period.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
