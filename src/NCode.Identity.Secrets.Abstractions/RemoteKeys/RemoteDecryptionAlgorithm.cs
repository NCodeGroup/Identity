// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.RemoteKeys;

/// <summary>
/// Describes one row of the mapping from a JOSE key management algorithm to the way a key management service creates a
/// key and decrypts a content key with it.
/// </summary>
// See ADR-1471.
[PublicAPI]
public sealed record RemoteDecryptionAlgorithm : RemoteKeyAlgorithm
{
    /// <summary>
    /// Gets the key management service's name for the algorithm that decrypts a content key, used when the service
    /// decrypts.
    /// </summary>
    public required string EncryptionAlgorithm { get; init; }

    /// <inheritdoc />
    public override string Use => SecretKeyUses.Encryption;
}
