// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using NCode.Buffers;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.EdDsa.Keys;
using NCode.Identity.Jose.EdDsa.KeyTypes;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.EdDsa.Algorithms;

/// <summary>
/// The <c>EdDSA</c> (RFC 8037) <see cref="SignatureAlgorithm"/>, registered under three <c>JOSE</c> algorithm codes:
/// the polymorphic <c>EdDSA</c>, which selects the concrete curve (Ed25519 or Ed448) from the key's
/// <see cref="OkpSecretKey.CurveName"/> at signing and verification time, and the fully-specified <c>Ed25519</c> and
/// <c>Ed448</c>, each of which accepts only keys on its own curve. An <c>OKP</c> key on a key-agreement curve
/// (X25519, X448) is never accepted, even though its key size matches an Ed25519 key's.
/// </summary>
/// <param name="code">The <c>JOSE</c> algorithm code.</param>
/// <param name="curve">The curve the algorithm is fixed to, or <see langword="null"/> to accept either curve.</param>
internal sealed class EdDsaSignatureAlgorithm(string code, EdwardsOkpCurve? curve)
    : SignatureAlgorithm
{
    private EdwardsOkpCurve? Curve { get; } = curve;

    /// <inheritdoc />
    public override string Code { get; } = code;

    /// <inheritdoc />
    public override IReadOnlyList<AlgorithmKeyType> KeyTypes { get; } =
    [
        AlgorithmKeyType.OnCurves(
            typeof(OkpSecretKey),
            (
                curve is not null
                    ? [curve]
                    : (EdwardsOkpCurve[])[OkpCurves.Ed25519, OkpCurves.Ed448]
            ).Select(signingCurve => new AlgorithmCurve(
                signingCurve.Name,
                signingCurve.KeySizeBits
            ))
        ),
    ];

    private IReadOnlyList<KeySizes> KeyBitSizes => KeyTypes[0].KeyBitSizes;

    // See ADR-1390: only a signing curve resolves, so an X25519 or X448 key never signs, even when it reaches the
    // algorithm without its curve having been checked.
    private bool TryResolveCurve(
        OkpSecretKey key,
        [NotNullWhen(true)] out EdwardsOkpCurve? resolved
    )
    {
        if (
            OkpCurves.TryGetByName(key.CurveName, out resolved)
            && (Curve is null || ReferenceEquals(resolved, Curve))
        )
        {
            return true;
        }

        resolved = null;
        return false;
    }

    /// <inheritdoc />
    public override HashFunctionDelegate GetTokenHashFunction(
        SecretKey secretKey,
        out int hashSizeBytes
    )
    {
        var okpSecretKey = secretKey.Validate<OkpSecretKey>(KeyBitSizes);

        if (!TryResolveCurve(okpSecretKey, out var resolved))
        {
            throw new InvalidOperationException(
                $"The OKP curve '{okpSecretKey.CurveName}' is not supported by the '{Code}' algorithm."
            );
        }

        return resolved.GetTokenHashFunction(out hashSizeBytes);
    }

    /// <summary>
    /// Gets the <see cref="HashAlgorithmName"/> used for derived hashes (for example OpenID Connect <c>at_hash</c> /
    /// <c>c_hash</c>). <c>EdDSA</c> hashes internally; SHA-512 is the conventional companion hash.
    /// </summary>
    public override HashAlgorithmName HashAlgorithmName => HashAlgorithmName.SHA512;

    /// <inheritdoc />
    public override int GetSignatureSizeBytes(int keySizeBits) =>
        OkpCurves.TryGetSigningCurveByKeySizeBits(keySizeBits, out var curve)
            ? curve.SignatureSize
            : throw new ArgumentOutOfRangeException(
                nameof(keySizeBits),
                keySizeBits,
                "The key size does not correspond to a supported OKP curve."
            );

    /// <inheritdoc />
    public override bool TrySign(
        SecretKey secretKey,
        ReadOnlySpan<byte> inputData,
        Span<byte> signature,
        out int bytesWritten
    )
    {
        var okpSecretKey = secretKey.Validate<OkpSecretKey>(KeyBitSizes);

        if (!TryResolveCurve(okpSecretKey, out var curve))
        {
            throw new InvalidOperationException(
                $"The OKP curve '{okpSecretKey.CurveName}' is not supported by the '{Code}' algorithm."
            );
        }

        if (!okpSecretKey.HasPrivateKey)
        {
            throw new InvalidOperationException(
                "The OKP secret key holds only the public half and cannot sign."
            );
        }

        if (signature.Length < curve.SignatureSize)
        {
            bytesWritten = 0;
            return false;
        }

        Span<byte> seed = stackalloc byte[curve.PrivateKeySize];
        try
        {
            var writer = new FixedSpanBufferWriter<byte>(seed);
            okpSecretKey.ExportPrivateKey(ref writer);
            curve.Sign(seed, inputData, signature[..curve.SignatureSize]);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(seed);
        }

        bytesWritten = curve.SignatureSize;
        return true;
    }

    /// <inheritdoc />
    public override bool Verify(
        SecretKey secretKey,
        ReadOnlySpan<byte> inputData,
        ReadOnlySpan<byte> signature
    )
    {
        var okpSecretKey = secretKey.Validate<OkpSecretKey>(KeyBitSizes);

        if (!TryResolveCurve(okpSecretKey, out var curve))
        {
            throw new InvalidOperationException(
                $"The OKP curve '{okpSecretKey.CurveName}' is not supported by the '{Code}' algorithm."
            );
        }

        if (signature.Length != curve.SignatureSize)
        {
            return false;
        }

        Span<byte> publicKey = stackalloc byte[curve.PublicKeySize];
        var writer = new FixedSpanBufferWriter<byte>(publicKey);
        okpSecretKey.ExportPublicKey(ref writer);

        return curve.Verify(publicKey, inputData, signature);
    }
}
