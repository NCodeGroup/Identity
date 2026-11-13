// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;

namespace NCode.Identity.OpenId.Setup;

/// <summary>
/// Checks when the host starts that <see cref="SetupOptions"/> holds a configured token of at least
/// <see cref="SetupDefaults.MinimumTokenLength"/> characters, a generated-token lifetime between one minute and
/// twenty-four hours, a root administrator that names both a username and a password or neither, and, when the
/// management console is configured, a console client with a client identifier, an https origin and a JWK Set, and,
/// when the automation client is configured, a client identifier that differs from the console's and a JWK Set. The
/// check reports every failure at once, each naming its configuration key.
/// </summary>
internal sealed class SetupOptionsValidator : IValidateOptions<SetupOptions>
{
    private const int MaximumClientIdLength = 255;

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

        ValidateManagementConsole(options.ManagementConsole, failures);
        ValidateAutomation(options, failures);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateManagementConsole(
        SetupManagementConsoleOptions console,
        List<string> failures
    )
    {
        if (!console.IsConfigured)
            return;

        failures.AddRange(SetupManagementConsoleRules.Validate(console));
    }

    private static void ValidateAutomation(SetupOptions options, List<string> failures)
    {
        var automation = options.Automation;
        if (!automation.IsConfigured)
            return;

        const string section = $"{SetupOptions.SectionName}:{nameof(SetupOptions.Automation)}";

        if (string.IsNullOrWhiteSpace(automation.ClientId))
        {
            failures.Add($"'{section}:{nameof(SetupAutomationOptions.ClientId)}' is required.");
        }
        else if (!IsClientId(automation.ClientId))
        {
            failures.Add(
                $"'{section}:{nameof(SetupAutomationOptions.ClientId)}' must be 1 to {MaximumClientIdLength} "
                    + "unreserved URI characters."
            );
        }
        else if (
            options.ManagementConsole.IsConfigured
            && string.Equals(
                automation.ClientId,
                options.ManagementConsole.ClientId,
                StringComparison.Ordinal
            )
        )
        {
            failures.Add(
                $"'{section}:{nameof(SetupAutomationOptions.ClientId)}' must differ from "
                    + $"'{SetupOptions.SectionName}:{nameof(SetupOptions.ManagementConsole)}:{nameof(SetupManagementConsoleOptions.ClientId)}'."
            );
        }

        if (string.IsNullOrWhiteSpace(automation.Jwks))
        {
            failures.Add($"'{section}:{nameof(SetupAutomationOptions.Jwks)}' is required.");
        }
        else if (!SetupJwksRule.IsKeySet(automation.Jwks))
        {
            failures.Add(
                $"'{section}:{nameof(SetupAutomationOptions.Jwks)}' must be a JSON object with a non-empty "
                    + "'keys' array."
            );
        }
    }

    // The unreserved characters of RFC 3986 section 2.3, as a client_id of the management API admits.
    private static bool IsClientId(string value)
    {
        if (value.Length > MaximumClientIdLength)
            return false;

        foreach (var character in value)
        {
            var isUnreserved =
                character is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9')
                || character is '-' or '.' or '_' or '~';
            if (!isUnreserved)
                return false;
        }

        return true;
    }
}
