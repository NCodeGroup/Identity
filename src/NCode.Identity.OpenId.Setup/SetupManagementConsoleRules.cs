// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Setup;

/// <summary>
/// The rules a <see cref="SetupManagementConsoleOptions"/> satisfies before setup registers the management console's
/// client: a client identifier, an https origin and a JWK Set. <see cref="SetupOptionsValidator"/> applies them to the
/// configured console and <see cref="SetupApiHandler"/> applies them to the console a setup request carries.
/// </summary>
internal static class SetupManagementConsoleRules
{
    private const string Section =
        $"{SetupOptions.SectionName}:{nameof(SetupOptions.ManagementConsole)}";

    /// <summary>
    /// Lists every rule the <paramref name="console"/> breaks, worded for the configuration or for the setup wizard.
    /// </summary>
    /// <param name="console">The management console's client to check.</param>
    /// <param name="namesConfigurationKeys"><see langword="true"/> to word each failure with the configuration key
    /// it breaks; <see langword="false"/> to word it with the setup wizard's field.</param>
    /// <returns>The failures, empty when the <paramref name="console"/> satisfies every rule.</returns>
    public static IReadOnlyList<string> Validate(
        SetupManagementConsoleOptions console,
        bool namesConfigurationKeys
    )
    {
        List<string> failures = [];

        if (string.IsNullOrWhiteSpace(console.ClientId))
        {
            failures.Add(
                namesConfigurationKeys
                    ? $"'{Section}:{nameof(SetupManagementConsoleOptions.ClientId)}' must not be empty."
                    : "The console's client ID must not be empty."
            );
        }

        if (string.IsNullOrWhiteSpace(console.BaseUrl))
        {
            failures.Add(
                namesConfigurationKeys
                    ? $"'{Section}:{nameof(SetupManagementConsoleOptions.BaseUrl)}' is required."
                    : "The console URL is required."
            );
        }
        else if (!IsHttpsOrigin(console.BaseUrl))
        {
            failures.Add(
                namesConfigurationKeys
                    ? $"'{Section}:{nameof(SetupManagementConsoleOptions.BaseUrl)}' must be an absolute https origin "
                        + "without a path, query or fragment."
                    : "The console URL must be an absolute https origin without a path, query or fragment, "
                        + "such as https://console.example.com."
            );
        }

        if (string.IsNullOrWhiteSpace(console.Jwks))
        {
            failures.Add(
                namesConfigurationKeys
                    ? $"'{Section}:{nameof(SetupManagementConsoleOptions.Jwks)}' is required."
                    : "The public JWK Set is required."
            );
        }
        else if (!SetupJwksRule.IsKeySet(console.Jwks))
        {
            failures.Add(
                namesConfigurationKeys
                    ? $"'{Section}:{nameof(SetupManagementConsoleOptions.Jwks)}' must be a JSON object with a "
                        + "non-empty 'keys' array."
                    : "The public JWK Set must be a JSON object with a non-empty keys array."
            );
        }

        return failures;
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
}
