// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Buffers;

namespace NCode.Identity.Secrets.RemoteKeys;

/// <summary>
/// Signs through the <see cref="IRemoteKeyProvider"/> of a <see cref="RemoteKeyBinding"/> and returns the signature in
/// the form that JOSE and COSE carry.
/// </summary>
// See ADR-1471.
internal static class RemoteKeySigning
{
    private const int HashMaxSizeBytes = 64;

    /// <summary>
    /// Signs the input with the bound remote key and writes the signature to <paramref name="signature"/>.
    /// </summary>
    /// <param name="binding">The remote key, its provider and the row of the mapping vocabulary that it was created for.</param>
    /// <param name="signingInput">The data to sign.</param>
    /// <param name="signature">Receives the signature.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of bytes written to <paramref name="signature"/>.</returns>
    /// <exception cref="CryptographicException">
    /// The signature that the provider returns does not fit <paramref name="signature"/>, or is not a valid DER
    /// encoded ECDSA signature when the row says that it is.
    /// </exception>
    /// <exception cref="InvalidOperationException">The binding's row is not a signing row.</exception>
    public static async ValueTask<int> SignAsync(
        RemoteKeyBinding binding,
        ReadOnlyMemory<byte> signingInput,
        Memory<byte> signature,
        CancellationToken cancellationToken
    )
    {
        if (binding.Algorithm is not RemoteSigningAlgorithm algorithm)
            throw new InvalidOperationException(
                $"The remote key is bound to the '{binding.Algorithm.Algorithm}' row, which does not sign."
            );

        if (algorithm.DigestAlgorithm is not { } digestAlgorithm)
            return await SignDataAsync(
                binding,
                algorithm,
                signingInput,
                signature,
                cancellationToken
            );

        using var hashLease = BufferFactory.Rent(
            HashMaxSizeBytes,
            isSensitive: false,
            out Memory<byte> hash
        );
        var hashLength = CryptographicOperations.HashData(
            digestAlgorithm,
            signingInput.Span,
            hash.Span
        );

        return await SignDataAsync(
            binding,
            algorithm,
            hash[..hashLength],
            signature,
            cancellationToken
        );
    }

    private static async ValueTask<int> SignDataAsync(
        RemoteKeyBinding binding,
        RemoteSigningAlgorithm algorithm,
        ReadOnlyMemory<byte> data,
        Memory<byte> signature,
        CancellationToken cancellationToken
    )
    {
        var request = new RemoteSignatureRequest
        {
            KeyId = binding.Reference.KeyId,
            Algorithm = algorithm,
            Data = data,
        };

        if (!algorithm.IsDerEncodedSignature)
        {
            var written = await binding.Provider.SignAsync(request, signature, cancellationToken);
            return CheckWritten(written, signature.Length);
        }

        var fieldSizeBytes = (algorithm.KeySizeBits + 7) / 8;

        // Each INTEGER adds a sign byte, a tag and a length; the SEQUENCE adds a tag and a length.
        using var derLease = BufferFactory.Rent(
            checked(2 * fieldSizeBytes + 12),
            isSensitive: false,
            out Memory<byte> der
        );
        var derWritten = await binding.Provider.SignAsync(request, der, cancellationToken);
        derWritten = CheckWritten(derWritten, der.Length);

        return EcdsaSignatureConverter.ConvertDerToIeeeP1363(
            der.Span[..derWritten],
            fieldSizeBytes,
            signature.Span
        );
    }

    private static int CheckWritten(int written, int bufferLength)
    {
        if (written < 0 || written > bufferLength)
            throw new CryptographicException(
                "The provider reported a signature length that does not fit the buffer."
            );

        return written;
    }
}
