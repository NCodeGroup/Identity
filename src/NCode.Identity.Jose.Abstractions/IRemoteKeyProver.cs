// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.Jose;

/// <summary>
/// Proves that a key a key management service holds also holds the private half of the public key the service
/// returned. The key's row selects the proof: a signature algorithm signs 32 random bytes through the service, which
/// <see cref="Algorithms.SignatureAlgorithm.SignAsync"/> verifies with the public half; a key-management algorithm
/// decrypts 32 random bytes that are encrypted to the public half as a compact JWE (<c>A256GCM</c>).
/// </summary>
// See ADR-1479.
[PublicAPI]
public interface IRemoteKeyProver
{
    /// <summary>
    /// Proves that the key holds the private half of its public half.
    /// </summary>
    /// <param name="secretKey">The <see cref="IRemoteSecretKey"/> to prove.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that completes when the key is proved.</returns>
    /// <exception cref="ArgumentException">The key is not an <see cref="IRemoteSecretKey"/>.</exception>
    /// <exception cref="RemoteKeyRefusedException">
    /// The key's algorithm is not a registered signature or key-management algorithm, or the signature or the
    /// decryption does not prove that the key holds the private half.
    /// </exception>
    ValueTask ProveAsync(SecretKey secretKey, CancellationToken cancellationToken);
}
