// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Bff;

/// <summary>
/// An opaque session record as an <see cref="IBffSessionStore"/> holds it: plaintext metadata and an encrypted payload.
/// </summary>
/// <param name="storeKey">The key of the record in the store.</param>
/// <param name="version">The version of the record; the store bumps it on every replace.</param>
/// <param name="metadata">The plaintext metadata.</param>
/// <param name="protectedPayload">The encrypted payload.</param>
[PublicAPI]
public sealed class BffSessionRecord(
    string storeKey,
    long version,
    BffSessionMetadata metadata,
    ReadOnlyMemory<byte> protectedPayload
)
{
    /// <summary>
    /// Gets the key of the record in the store.
    /// </summary>
    public string StoreKey { get; } = ValidateStoreKey(storeKey, nameof(storeKey));

    /// <summary>
    /// Gets the version of the record.
    /// </summary>
    public long Version { get; } = ValidateVersion(version, nameof(version));

    /// <summary>
    /// Gets the plaintext metadata.
    /// </summary>
    public BffSessionMetadata Metadata { get; } =
        metadata ?? throw new ArgumentNullException(nameof(metadata));

    /// <summary>
    /// Gets the encrypted payload.
    /// </summary>
    public ReadOnlyMemory<byte> ProtectedPayload { get; } = protectedPayload;

    private static string ValidateStoreKey(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrEmpty(value, paramName);
        return value;
    }

    private static long ValidateVersion(long value, string paramName)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value, paramName);
        return value;
    }
}
