// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;

namespace NCode.Identity.Secrets.RemoteKeys;

/// <summary>
/// Decrypts and agrees keys through the <see cref="IRemoteKeyProvider"/> of a <see cref="RemoteKeyBinding"/>.
/// </summary>
// See ADR-1477.
internal static class RemoteKeyDecryption
{
    /// <summary>
    /// Decrypts the ciphertext with the bound remote key and writes the plaintext to <paramref name="plaintext"/>.
    /// </summary>
    /// <param name="binding">The remote key, its provider and the row of the mapping vocabulary that it was created for.</param>
    /// <param name="ciphertext">The data to decrypt.</param>
    /// <param name="plaintext">Receives the plaintext.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of bytes written to <paramref name="plaintext"/>.</returns>
    /// <exception cref="CryptographicException">
    /// The provider refuses the ciphertext or reports a length that does not fit <paramref name="plaintext"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">The binding's row is not a decryption row.</exception>
    public static async ValueTask<int> DecryptAsync(
        RemoteKeyBinding binding,
        ReadOnlyMemory<byte> ciphertext,
        Memory<byte> plaintext,
        CancellationToken cancellationToken
    )
    {
        if (binding.Algorithm is not RemoteDecryptionAlgorithm algorithm)
            throw new InvalidOperationException(
                $"The remote key is bound to the '{binding.Algorithm.Algorithm}' row, which does not decrypt."
            );

        var request = new RemoteDecryptionRequest
        {
            KeyId = binding.Reference.KeyId,
            Algorithm = algorithm,
            Ciphertext = ciphertext,
        };

        var written = await binding.Provider.DecryptAsync(request, plaintext, cancellationToken);
        return CheckWritten(written, plaintext.Length, "plaintext");
    }

    /// <summary>
    /// Agrees a shared secret between the bound remote key and the peer's public key and writes it to
    /// <paramref name="sharedSecret"/>.
    /// </summary>
    /// <param name="binding">The remote key, its provider and the row of the mapping vocabulary that it was created for.</param>
    /// <param name="peerSubjectPublicKeyInfo">The DER-encoded <c>SubjectPublicKeyInfo</c> of the peer's public key.</param>
    /// <param name="sharedSecret">Receives the shared secret.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of bytes written to <paramref name="sharedSecret"/>.</returns>
    /// <exception cref="CryptographicException">
    /// The provider refuses the peer key or reports a length that does not fit <paramref name="sharedSecret"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">The binding's row is not a key agreement row.</exception>
    public static async ValueTask<int> DeriveSharedSecretAsync(
        RemoteKeyBinding binding,
        ReadOnlyMemory<byte> peerSubjectPublicKeyInfo,
        Memory<byte> sharedSecret,
        CancellationToken cancellationToken
    )
    {
        if (binding.Algorithm is not RemoteKeyAgreementAlgorithm algorithm)
            throw new InvalidOperationException(
                $"The remote key is bound to the '{binding.Algorithm.Algorithm}' row, which does not agree keys."
            );

        var request = new RemoteKeyAgreementRequest
        {
            KeyId = binding.Reference.KeyId,
            Algorithm = algorithm,
            PeerSubjectPublicKeyInfo = peerSubjectPublicKeyInfo,
        };

        var written = await binding.Provider.DeriveSharedSecretAsync(
            request,
            sharedSecret,
            cancellationToken
        );
        return CheckWritten(written, sharedSecret.Length, "shared secret");
    }

    private static int CheckWritten(int written, int bufferLength, string what)
    {
        if (written < 0 || written > bufferLength)
            throw new CryptographicException(
                $"The provider reported a {what} length that does not fit the buffer."
            );

        return written;
    }
}
