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
    /// Lists every rule the <paramref name="console"/> breaks, each naming its configuration key.
    /// </summary>
    /// <param name="console">The management console's client to check.</param>
    /// <returns>The failures, empty when the <paramref name="console"/> satisfies every rule.</returns>
    public static IReadOnlyList<string> Validate(SetupManagementConsoleOptions console)
    {
        List<string> failures = [];

        if (string.IsNullOrWhiteSpace(console.ClientId))
        {
            failures.Add(
                $"'{Section}:{nameof(SetupManagementConsoleOptions.ClientId)}' must not be empty."
            );
        }

        if (string.IsNullOrWhiteSpace(console.BaseUrl))
        {
            failures.Add(
                $"'{Section}:{nameof(SetupManagementConsoleOptions.BaseUrl)}' is required."
            );
        }
        else if (!IsHttpsOrigin(console.BaseUrl))
        {
            failures.Add(
                $"'{Section}:{nameof(SetupManagementConsoleOptions.BaseUrl)}' must be an absolute https origin "
                    + "without a path, query or fragment."
            );
        }

        if (string.IsNullOrWhiteSpace(console.Jwks))
        {
            failures.Add($"'{Section}:{nameof(SetupManagementConsoleOptions.Jwks)}' is required.");
        }
        else if (!SetupJwksRule.IsKeySet(console.Jwks))
        {
            failures.Add(
                $"'{Section}:{nameof(SetupManagementConsoleOptions.Jwks)}' must be a JSON object with a non-empty "
                    + "'keys' array."
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
