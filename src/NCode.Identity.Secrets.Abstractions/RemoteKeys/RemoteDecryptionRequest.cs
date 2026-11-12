// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.RemoteKeys;

/// <summary>
/// Describes one decryption for a <see cref="IRemoteKeyProvider"/> to perform.
/// </summary>
// See ADR-1471.
[PublicAPI]
public sealed class RemoteDecryptionRequest
{
    /// <summary>
    /// Gets the key management service's identifier for the key to decrypt with.
    /// </summary>
    public required string KeyId { get; init; }

    /// <summary>
    /// Gets the row of the mapping vocabulary that selects the key management service's decryption algorithm.
    /// </summary>
    public required RemoteDecryptionAlgorithm Algorithm { get; init; }

    /// <summary>
    /// Gets the ciphertext to decrypt. The memory is valid until the call to
    /// <see cref="IRemoteKeyProvider.DecryptAsync"/> completes.
    /// </summary>
    public required ReadOnlyMemory<byte> Ciphertext { get; init; }
}
