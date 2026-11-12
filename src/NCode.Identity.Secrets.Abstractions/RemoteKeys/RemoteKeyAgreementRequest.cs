// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.RemoteKeys;

/// <summary>
/// Describes one key agreement for a <see cref="IRemoteKeyProvider"/> to perform.
/// </summary>
// See ADR-1471.
[PublicAPI]
public sealed class RemoteKeyAgreementRequest
{
    /// <summary>
    /// Gets the key management service's identifier for the key to agree with.
    /// </summary>
    public required string KeyId { get; init; }

    /// <summary>
    /// Gets the row of the mapping vocabulary that selects the key management service's key agreement algorithm.
    /// </summary>
    public required RemoteKeyAgreementAlgorithm Algorithm { get; init; }

    /// <summary>
    /// Gets the DER-encoded <c>SubjectPublicKeyInfo</c> of the peer's public key on the key's curve. The memory is
    /// valid until the call to <see cref="IRemoteKeyProvider.DeriveSharedSecretAsync"/> completes.
    /// </summary>
    public required ReadOnlyMemory<byte> PeerSubjectPublicKeyInfo { get; init; }
}
