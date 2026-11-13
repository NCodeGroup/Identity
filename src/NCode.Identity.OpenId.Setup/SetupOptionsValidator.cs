// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.Extensions.Options;

namespace NCode.Identity.OpenId.Setup;

/// <summary>
/// Checks when the host starts that <see cref="SetupOptions"/> holds a configured token of at least
/// <see cref="SetupDefaults.MinimumTokenLength"/> characters, a generated-token lifetime between one minute and
/// twenty-four hours, a root administrator that names both a username and a password or neither, and, when the
/// management console is configured, a console client with a client identifier, an https origin and a JWK Set. The
/// check reports every failure at once, each naming its configuration key.
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

        ValidateManagementConsole(options.ManagementConsole, failures);

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

        const string section =
            $"{SetupOptions.SectionName}:{nameof(SetupOptions.ManagementConsole)}";

        if (string.IsNullOrWhiteSpace(console.ClientId))
        {
            failures.Add(
                $"'{section}:{nameof(SetupManagementConsoleOptions.ClientId)}' must not be empty."
            );
        }

        if (string.IsNullOrWhiteSpace(console.BaseUrl))
        {
            failures.Add(
                $"'{section}:{nameof(SetupManagementConsoleOptions.BaseUrl)}' is required."
            );
        }
        else if (!IsHttpsOrigin(console.BaseUrl))
        {
            failures.Add(
                $"'{section}:{nameof(SetupManagementConsoleOptions.BaseUrl)}' must be an absolute https origin "
                    + "without a path, query or fragment."
            );
        }

        if (string.IsNullOrWhiteSpace(console.Jwks))
        {
            failures.Add($"'{section}:{nameof(SetupManagementConsoleOptions.Jwks)}' is required.");
        }
        else if (!IsKeySet(console.Jwks))
        {
            failures.Add(
                $"'{section}:{nameof(SetupManagementConsoleOptions.Jwks)}' must be a JSON object with a non-empty "
                    + "'keys' array."
            );
        }
    }

    private static bool IsHttpsOrigin(string value)
    {
        if (
            value.Contains('?', StringComparison.Ordinal)
            || value.Contains('#', StringComparison.Ordinal)
        )
            return false;

        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && uri.UserInfo.Length == 0
            && uri.AbsolutePath == "/";
    }

    private static bool IsKeySet(string value)
    {
        try
        {
            var root = JsonElement.Parse(value);
            return root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("keys", out var keys)
                && keys.ValueKind == JsonValueKind.Array
                && keys.GetArrayLength() > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
