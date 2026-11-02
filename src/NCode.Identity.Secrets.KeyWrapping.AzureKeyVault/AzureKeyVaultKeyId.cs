// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace NCode.Identity.Secrets.KeyWrapping.AzureKeyVault;

/// <summary>
/// Contains the rules for the identifier of a key in Azure Key Vault or Managed HSM: whether a value is one, and whether
/// it names a key version.
/// </summary>
// See ADR-1260.
internal static partial class AzureKeyVaultKeyId
{
    // '\z' rather than '$', which also matches before a trailing newline; the patterns accept ASCII letters and digits
    // only.
    [GeneratedRegex(
        @"^https://[A-Za-z0-9-]+(\.[A-Za-z0-9-]+)+/keys/[A-Za-z0-9-]{1,127}(/[A-Za-z0-9]{32})?\z"
    )]
    private static partial Regex KeyPattern();

    [GeneratedRegex(
        @"^https://[A-Za-z0-9-]+(\.[A-Za-z0-9-]+)+/keys/[A-Za-z0-9-]{1,127}/[A-Za-z0-9]{32}\z"
    )]
    private static partial Regex KeyVersionPattern();

    /// <summary>
    /// Determines whether the specified value is the identifier of a Key Vault or Managed HSM key, with or without a
    /// version.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> if the value is a key identifier; otherwise, <see langword="false"/>.</returns>
    public static bool IsKeyId([NotNullWhen(true)] string? value) =>
        value is not null && KeyPattern().IsMatch(value);

    /// <summary>
    /// Determines whether the specified value is the identifier of a Key Vault or Managed HSM key version.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> if the value is a key version identifier; otherwise, <see langword="false"/>.</returns>
    public static bool IsKeyVersionId([NotNullWhen(true)] string? value) =>
        value is not null && KeyVersionPattern().IsMatch(value);
}
