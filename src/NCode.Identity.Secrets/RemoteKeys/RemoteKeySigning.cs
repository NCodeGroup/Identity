// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;

namespace NCode.Identity.Secrets.RemoteKeys;

/// <summary>
/// Signs through the <see cref="IRemoteKeyProvider"/> of a <see cref="RemoteKeyBinding"/> and returns the signature in
/// the form that JOSE and COSE carry.
/// </summary>
// See ADR-1471.
internal static class RemoteKeySigning
{
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
    public static async ValueTask<int> SignAsync(
        RemoteKeyBinding binding,
        ReadOnlyMemory<byte> signingInput,
        Memory<byte> signature,
        CancellationToken cancellationToken
    )
    {
        var algorithm = binding.Algorithm;

        ReadOnlyMemory<byte> data = algorithm.DigestAlgorithm is { } digestAlgorithm
            ? CryptographicOperations.HashData(digestAlgorithm, signingInput.Span)
            : signingInput;

        var providerSignature = await binding.Provider.SignAsync(
            new RemoteSignatureRequest
            {
                KeyId = binding.Reference.KeyId,
                Algorithm = algorithm,
                Data = data,
            },
            cancellationToken
        );

        if (algorithm.IsDerEncodedSignature)
        {
            var fieldSizeBytes = (algorithm.KeySizeBits + 7) / 8;
            return EcdsaSignatureConverter.ConvertDerToIeeeP1363(
                providerSignature,
                fieldSizeBytes,
                signature.Span
            );
        }

        if (providerSignature.Length > signature.Length)
            throw new CryptographicException("The destination is too small for the signature.");

        providerSignature.CopyTo(signature);
        return providerSignature.Length;
    }
}
