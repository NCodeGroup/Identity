// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace NCode.Identity.Secrets.GoogleCloudKms;

/// <summary>
/// Contains the rules for the resource name of a Google Cloud KMS key ring, crypto key or crypto key version: whether a
/// value is one, and which crypto key a version belongs to.
/// </summary>
// See ADR-1260.
internal static partial class GoogleCloudKmsKeyName
{
    private const string CryptoKeyVersionsSegment = "/cryptoKeyVersions/";

    // '\z' rather than '$', which also matches before a trailing newline, and '[0-9]' rather than '\d', which accepts
    // any Unicode digit.
    [GeneratedRegex(@"^projects/[a-z0-9.:-]+/locations/[a-z0-9-]+/keyRings/[A-Za-z0-9_-]{1,63}\z")]
    private static partial Regex KeyRingPattern();

    [GeneratedRegex(
        @"^projects/[a-z0-9.:-]+/locations/[a-z0-9-]+/keyRings/[A-Za-z0-9_-]{1,63}/cryptoKeys/[A-Za-z0-9_-]{1,63}\z"
    )]
    private static partial Regex CryptoKeyPattern();

    [GeneratedRegex(
        @"^projects/[a-z0-9.:-]+/locations/[a-z0-9-]+/keyRings/[A-Za-z0-9_-]{1,63}/cryptoKeys/[A-Za-z0-9_-]{1,63}/cryptoKeyVersions/[0-9]{1,19}\z"
    )]
    private static partial Regex CryptoKeyVersionPattern();

    /// <summary>
    /// Determines whether the specified value is the resource name of a Cloud KMS key ring.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> if the value is a key ring resource name; otherwise, <see langword="false"/>.</returns>
    public static bool IsKeyRingName([NotNullWhen(true)] string? value) =>
        value is not null && KeyRingPattern().IsMatch(value);

    /// <summary>
    /// Determines whether the specified value is the resource name of a Cloud KMS crypto key, without a version.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> if the value is a crypto key resource name; otherwise, <see langword="false"/>.</returns>
    public static bool IsCryptoKeyName([NotNullWhen(true)] string? value) =>
        value is not null && CryptoKeyPattern().IsMatch(value);

    /// <summary>
    /// Determines whether the specified value is the resource name of a Cloud KMS crypto key version.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> if the value is a crypto key version resource name; otherwise, <see langword="false"/>.</returns>
    public static bool IsCryptoKeyVersionName([NotNullWhen(true)] string? value) =>
        value is not null && CryptoKeyVersionPattern().IsMatch(value);

    /// <summary>
    /// Gets the resource name of the crypto key that the specified crypto key version belongs to. The name must already
    /// have been validated with <see cref="IsCryptoKeyVersionName"/>.
    /// </summary>
    /// <param name="cryptoKeyVersionName">The resource name of a crypto key version.</param>
    /// <returns>The resource name of the crypto key, such as
    /// <c>projects/my-project/locations/us-central1/keyRings/ncode-identity/cryptoKeys/root</c>.</returns>
    public static string GetCryptoKeyName(string cryptoKeyVersionName)
    {
        var end = cryptoKeyVersionName.LastIndexOf(
            CryptoKeyVersionsSegment,
            StringComparison.Ordinal
        );
        return cryptoKeyVersionName.Substring(0, end);
    }
}
