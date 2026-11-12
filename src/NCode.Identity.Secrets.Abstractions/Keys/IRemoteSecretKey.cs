// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.Secrets.Keys;

/// <summary>
/// Implemented by a <see cref="SecretKey"/> whose private half a key management service holds. The key carries the
/// service's public half like any other asymmetric key and signs, decrypts or agrees by asking the service, as its
/// row's kind says.
/// </summary>
// See ADR-1471.
[PublicAPI]
public interface IRemoteSecretKey
{
    /// <summary>
    /// Gets the binding of the key to its provider, reference and mapping row.
    /// </summary>
    RemoteKeyBinding Binding { get; }

    /// <summary>
    /// Signs the signing input with the key management service's key and writes the signature in the form that the
    /// JOSE algorithm defines.
    /// </summary>
    /// <param name="algorithm">The JOSE <c>alg</c> value of the signature algorithm.</param>
    /// <param name="signingInput">The data to sign.</param>
    /// <param name="signature">The buffer that receives the signature.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of bytes written to <paramref name="signature"/>.</returns>
    ValueTask<int> SignAsync(
        string algorithm,
        ReadOnlyMemory<byte> signingInput,
        Memory<byte> signature,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Decrypts the ciphertext with the key management service's key and writes the plaintext into the caller's
    /// buffer.
    /// </summary>
    /// <param name="algorithm">The JOSE <c>alg</c> value of the key management algorithm.</param>
    /// <param name="ciphertext">The data to decrypt.</param>
    /// <param name="plaintext">The buffer that receives the plaintext.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of bytes written to <paramref name="plaintext"/>.</returns>
    // See ADR-1471.
    ValueTask<int> DecryptAsync(
        string algorithm,
        ReadOnlyMemory<byte> ciphertext,
        Memory<byte> plaintext,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Agrees a shared secret between the key management service's key and the peer's public key and writes the raw
    /// shared secret into the caller's buffer.
    /// </summary>
    /// <param name="algorithm">The JOSE <c>alg</c> value of the key management algorithm.</param>
    /// <param name="peerSubjectPublicKeyInfo">The DER-encoded <c>SubjectPublicKeyInfo</c> of the peer's public key.</param>
    /// <param name="sharedSecret">The buffer that receives the shared secret.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of bytes written to <paramref name="sharedSecret"/>.</returns>
    // See ADR-1471.
    ValueTask<int> DeriveSharedSecretAsync(
        string algorithm,
        ReadOnlyMemory<byte> peerSubjectPublicKeyInfo,
        Memory<byte> sharedSecret,
        CancellationToken cancellationToken
    );
}
