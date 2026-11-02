// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.Algorithms.Signature;

/// <summary>
/// A post-quantum <c>ML-DSA</c> (FIPS 204 / RFC 9964) <see cref="SignatureAlgorithm"/>. One instance is registered per
/// parameter set (<c>ML-DSA-44</c>, <c>ML-DSA-65</c>, <c>ML-DSA-87</c>); each accepts only an
/// <see cref="AkpSecretKey"/> whose parameter set matches. ML-DSA hashes the message internally, so no external hash
/// is applied before signing; the context string is empty per RFC 9964.
/// </summary>
internal sealed class MlDsaSignatureAlgorithm(
    string code,
    MLDsaAlgorithm algorithm,
    HashAlgorithmName hashAlgorithmName
) : SignatureAlgorithm
{
    /// <inheritdoc />
    public override string Code => code;

    /// <inheritdoc />
    public override IReadOnlyList<AlgorithmKeyType> KeyTypes { get; } =
    [
        new(
            typeof(AkpSecretKey),
            [
                new KeySizes(
                    algorithm.PublicKeySizeInBytes * 8,
                    algorithm.PublicKeySizeInBytes * 8,
                    0
                ),
            ]
        ),
    ];

    private IReadOnlyList<KeySizes> KeyBitSizes => KeyTypes[0].KeyBitSizes;

    /// <summary>
    /// Gets the <see cref="HashAlgorithmName"/> used for derived hashes (for example OpenID Connect <c>at_hash</c> /
    /// <c>c_hash</c>). ML-DSA hashes internally; the companion hash matches the parameter set's security category.
    /// </summary>
    public override HashAlgorithmName HashAlgorithmName => hashAlgorithmName;

    /// <inheritdoc />
    public override int GetSignatureSizeBytes(int keySizeBits) => algorithm.SignatureSizeInBytes;

    /// <inheritdoc />
    public override bool TrySign(
        SecretKey secretKey,
        ReadOnlySpan<byte> inputData,
        Span<byte> signature,
        out int bytesWritten
    )
    {
        var akpSecretKey = secretKey.Validate<AkpSecretKey>(KeyBitSizes);

        if (!akpSecretKey.HasPrivateKey)
        {
            throw new InvalidOperationException(
                "The AKP secret key holds only the public half and cannot sign."
            );
        }

        if (signature.Length < algorithm.SignatureSizeInBytes)
        {
            bytesWritten = 0;
            return false;
        }

        using var key = akpSecretKey.ExportMLDsa();
        key.SignData(inputData, signature[..algorithm.SignatureSizeInBytes]);

        bytesWritten = algorithm.SignatureSizeInBytes;
        return true;
    }

    /// <inheritdoc />
    public override bool Verify(
        SecretKey secretKey,
        ReadOnlySpan<byte> inputData,
        ReadOnlySpan<byte> signature
    )
    {
        var akpSecretKey = secretKey.Validate<AkpSecretKey>(KeyBitSizes);

        if (signature.Length != algorithm.SignatureSizeInBytes)
        {
            return false;
        }

        using var key = akpSecretKey.ExportMLDsa();
        return key.VerifyData(inputData, signature);
    }
}
