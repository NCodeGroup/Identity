// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;

namespace NCode.Identity.OpenId.Setup;

/// <summary>
/// Checks when the host starts that <see cref="SetupOptions"/> holds a configured token of at least
/// <see cref="SetupDefaults.MinimumTokenLength"/> characters, a generated-token lifetime between one minute and
/// twenty-four hours, and a root administrator that names both a username and a password or neither. The check reports
/// every failure at once, each naming its configuration key.
/// </summary>
internal sealed class SetupOptionsValidator : IValidateOptions<SetupOptions>
{
    internal static TimeSpan MinimumGeneratedTokenLifetime { get; } = TimeSpan.FromMinutes(1);
    internal static TimeSpan MaximumGeneratedTokenLifetime { get; } = TimeSpan.FromHours(24);

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, SetupOptions options)
    {
        List<string> failures = [];

        if (options.Token is not null && options.Token.Length < SetupDefaults.MinimumTokenLength)
        {
            failures.Add(
                $"'{SetupOptions.SectionName}:{nameof(SetupOptions.Token)}' must be at least "
                    + $"{SetupDefaults.MinimumTokenLength} characters long, or unset so that the host generates a token."
            );
        }

        if (
            options.GeneratedTokenLifetime < MinimumGeneratedTokenLifetime
            || options.GeneratedTokenLifetime > MaximumGeneratedTokenLifetime
        )
        {
            failures.Add(
                $"'{SetupOptions.SectionName}:{nameof(SetupOptions.GeneratedTokenLifetime)}' must be between "
                    + $"{MinimumGeneratedTokenLifetime} and {MaximumGeneratedTokenLifetime}."
            );
        }

        var hasUserName = !string.IsNullOrWhiteSpace(options.Root.UserName);
        var hasPassword = !string.IsNullOrWhiteSpace(options.Root.Password);
        if (hasUserName != hasPassword)
        {
            failures.Add(
                $"'{SetupOptions.SectionName}:{nameof(SetupOptions.Root)}' must set both "
                    + $"'{nameof(SetupRootOptions.UserName)}' and '{nameof(SetupRootOptions.Password)}', or neither."
            );
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
