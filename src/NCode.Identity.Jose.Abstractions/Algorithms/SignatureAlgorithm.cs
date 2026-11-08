// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using JetBrains.Annotations;
using NCode.Identity.Jose.Exceptions;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.Algorithms;

/// <summary>
/// Provides methods for all cryptographic digital signature algorithms.
/// </summary>
[PublicAPI]
public abstract class SignatureAlgorithm : KeyedAlgorithm
{
    /// <inheritdoc />
    public override AlgorithmType Type => AlgorithmType.DigitalSignature;

    /// <summary>
    /// Gets the <see cref="HashAlgorithmName"/> that is used by this digital signature algorithm.
    /// </summary>
    public abstract HashAlgorithmName HashAlgorithmName { get; }

    /// <summary>
    /// Gets the size, in bytes, of the digital signature given the size, in bits, of the signing key.
    /// </summary>
    public abstract int GetSignatureSizeBytes(int keySizeBits);

    /// <summary>
    /// Gets the hash function, and its output size in bytes, that OpenID Connect uses for the token hash claims
    /// (at_hash, c_hash, s_hash) of a token this algorithm signs with the given key. The default uses
    /// <see cref="HashAlgorithmName"/>.
    /// </summary>
    /// <param name="secretKey">Contains the key material for the cryptographic algorithm.</param>
    /// <param name="hashSizeBytes">The output size, in bytes, of the returned hash function.</param>
    /// <returns>The hash function to use for the token hash claims.</returns>
    public virtual HashFunctionDelegate GetTokenHashFunction(
        SecretKey secretKey,
        out int hashSizeBytes
    )
    {
        hashSizeBytes = HashAlgorithmName.GetHashSizeBits() >> 3;
        return HashAlgorithmName.GetHashFunction();
    }

    /// <summary>
    /// Computes a digital signature with key material that the process holds. A key whose private half a key
    /// management service holds signs through <see cref="SignAsync"/>.
    /// </summary>
    /// <param name="secretKey">Contains the key material for the cryptographic algorithm.</param>
    /// <param name="inputData">Contains the data to sign.</param>
    /// <param name="signature">Destination for the calculated signature.</param>
    /// <param name="bytesWritten">The number of bytes written to <paramref name="signature"/>.</param>
    /// <returns><c>true></c> if there was enough room in <paramref name="signature"/> to copy all computed bytes; otherwise, <c>false</c>.</returns>
    public abstract bool TrySign(
        SecretKey secretKey,
        ReadOnlySpan<byte> inputData,
        Span<byte> signature,
        out int bytesWritten
    );

    /// <summary>
    /// Computes a digital signature with a key that the process holds or that a key management service holds. A
    /// signature from a remote key is verified with the key's public half before it is returned. A local key signs
    /// synchronously, so the returned task is already completed.
    /// </summary>
    /// <param name="secretKey">Contains the key material for the cryptographic algorithm.</param>
    /// <param name="inputData">Contains the data to sign.</param>
    /// <param name="signature">Destination for the calculated signature.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of bytes written to <paramref name="signature"/>.</returns>
    /// <exception cref="JoseInvalidAlgorithmException">The remote key is not compatible with this algorithm.</exception>
    /// <exception cref="JoseException">The signature buffer is too small, or the remote key's signature does not verify.</exception>
    // See ADR-1472.
    public virtual ValueTask<int> SignAsync(
        SecretKey secretKey,
        ReadOnlyMemory<byte> inputData,
        Memory<byte> signature,
        CancellationToken cancellationToken
    )
    {
        if (secretKey is IRemoteSecretKey remoteKey)
        {
            if (!IsKeyCompatible(secretKey))
                throw new JoseInvalidAlgorithmException(
                    $"The key is not compatible with the algorithm '{Code}'."
                );

            return SignRemoteAsync(secretKey, remoteKey, inputData, signature, cancellationToken);
        }

        if (!TrySign(secretKey, inputData.Span, signature.Span, out var bytesWritten))
            throw new JoseException("The signature buffer is too small.");

        return ValueTask.FromResult(bytesWritten);
    }

    private async ValueTask<int> SignRemoteAsync(
        SecretKey secretKey,
        IRemoteSecretKey remoteKey,
        ReadOnlyMemory<byte> inputData,
        Memory<byte> signature,
        CancellationToken cancellationToken
    )
    {
        var bytesWritten = await remoteKey.SignAsync(Code, inputData, signature, cancellationToken);

        if (!IsRemoteSignatureValid(secretKey, inputData, signature, bytesWritten))
            throw new JoseException(
                $"The remote key '{remoteKey.Binding.Reference.KeyId}' returned a signature that its public key does not verify."
            );

        return bytesWritten;
    }

    private bool IsRemoteSignatureValid(
        SecretKey secretKey,
        ReadOnlyMemory<byte> inputData,
        Memory<byte> signature,
        int bytesWritten
    ) =>
        bytesWritten == GetSignatureSizeBytes(secretKey.KeySizeBits)
        && Verify(secretKey, inputData.Span, signature.Span[..bytesWritten]);

    /// <summary>
    /// Verifies a digital signature.
    /// </summary>
    /// <param name="secretKey">Contains the key material for the cryptographic algorithm.</param>
    /// <param name="inputData">Contains the data what was signed.</param>
    /// <param name="signature">Contains the digital signature to verify.</param>
    /// <returns><c>true</c> if the computed signature matches the <paramref name="signature"/> parameter; otherwise, <c>false</c>.</returns>
    public abstract bool Verify(
        SecretKey secretKey,
        ReadOnlySpan<byte> inputData,
        ReadOnlySpan<byte> signature
    );
}
