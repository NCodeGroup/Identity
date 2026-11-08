// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.RemoteKeys;

/// <summary>
/// Describes one signature for a <see cref="IRemoteKeyProvider"/> to produce.
/// </summary>
// See ADR-1471.
[PublicAPI]
public sealed class RemoteSignatureRequest
{
    /// <summary>
    /// Gets the key management service's identifier for the key to sign with.
    /// </summary>
    public required string KeyId { get; init; }

    /// <summary>
    /// Gets the row of the mapping vocabulary that selects the key management service's signing algorithm.
    /// </summary>
    public required RemoteSigningAlgorithm Algorithm { get; init; }

    /// <summary>
    /// Gets the data to sign: the digest when <see cref="RemoteSigningAlgorithm.DigestAlgorithm"/> of
    /// <see cref="Algorithm"/> is set, otherwise the whole signing input. The memory is valid until the call to
    /// <see cref="IRemoteKeyProvider.SignAsync"/> completes.
    /// </summary>
    public required ReadOnlyMemory<byte> Data { get; init; }
}
