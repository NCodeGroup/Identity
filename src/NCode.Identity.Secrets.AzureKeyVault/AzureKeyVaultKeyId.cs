// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace NCode.Identity.Secrets.AzureKeyVault;

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

    /// <summary>
    /// Determines whether the host of a key identifier ends with one of the suffixes, compared without regard to case.
    /// </summary>
    /// <param name="keyId">The identifier of a key, with or without a version.</param>
    /// <param name="hostSuffixes">The host suffixes, each starting with a dot.</param>
    /// <returns>
    /// <see langword="true"/> if the identifier has a host that ends with a suffix; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool IsOnHostEndingWith(string keyId, IEnumerable<string> hostSuffixes)
    {
        if (!Uri.TryCreate(keyId, UriKind.Absolute, out var uri))
            return false;

        var host = uri.Host;
        return hostSuffixes.Any(suffix =>
            host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
        );
    }

    /// <summary>
    /// Gets the identifier of a key without its version: a key version identifier loses its last segment, and an
    /// identifier without a version is returned as it is.
    /// </summary>
    /// <param name="keyId">The identifier of a key, with or without a version.</param>
    /// <returns>The identifier of the key without a version.</returns>
    public static string GetKeyIdWithoutVersion(string keyId) =>
        IsKeyVersionId(keyId) ? keyId[..keyId.LastIndexOf('/')] : keyId;

    /// <summary>
    /// Determines whether a key version identifier is a version of the key that a reference names. A reference that
    /// carries a version names that version only, and one that does not names every version of the key. The host and
    /// the names are compared without regard to case.
    /// </summary>
    /// <param name="keyVersionId">The identifier of a key version.</param>
    /// <param name="keyReference">The identifier of a key, with or without a version.</param>
    /// <returns>
    /// <see langword="true"/> if the version belongs to the referenced key; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool IsVersionOf(string keyVersionId, string keyReference) =>
        IsKeyVersionId(keyVersionId)
        && string.Equals(
            IsKeyVersionId(keyReference) ? keyVersionId : GetKeyIdWithoutVersion(keyVersionId),
            keyReference,
            StringComparison.OrdinalIgnoreCase
        );
}
