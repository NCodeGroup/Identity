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
/// <see cref="OkpSecretKey.Curve"/> at signing and verification time, and the fully-specified <c>Ed25519</c> and
/// <c>Ed448</c>, each of which accepts only keys on its own curve. An <c>OKP</c> key on a key-agreement curve
/// (X25519, X448) is never accepted, even though its key size matches an Ed25519 key's.
/// </summary>
/// <param name="code">The <c>JOSE</c> algorithm code.</param>
/// <param name="curve">The curve the algorithm is fixed to, or <see langword="null"/> to accept either curve.</param>
internal sealed class EdDsaSignatureAlgorithm(string code, OkpCurveInfo? curve) : SignatureAlgorithm
{
    private static IEnumerable<KeySizes> GetKeyBitSizes(OkpCurveInfo? curve) =>
        curve is not null
            ? [new KeySizes(minSize: curve.KeySizeBits, maxSize: curve.KeySizeBits, skipSize: 0)]
            :
            [
                new KeySizes(
                    minSize: OkpCurves.Ed25519KeySizeBits,
                    maxSize: OkpCurves.Ed25519KeySizeBits,
                    skipSize: 0
                ),
                new KeySizes(
                    minSize: OkpCurves.Ed448KeySizeBits,
                    maxSize: OkpCurves.Ed448KeySizeBits,
                    skipSize: 0
                ),
            ];

    private OkpCurveInfo? Curve { get; } = curve;

    /// <inheritdoc />
    public override string Code { get; } = code;

    /// <inheritdoc />
    public override IReadOnlyList<AlgorithmKeyType> KeyTypes { get; } =
    [
        new(typeof(OkpSecretKey), GetKeyBitSizes(curve))
        {
            Curves = curve is not null
                ? [new(curve.Name, curve.KeySizeBits)]
                :
                [
                    new(OkpCurves.Ed25519Name, OkpCurves.Ed25519KeySizeBits),
                    new(OkpCurves.Ed448Name, OkpCurves.Ed448KeySizeBits),
                ],
        },
    ];

    private IReadOnlyList<KeySizes> KeyBitSizes => KeyTypes[0].KeyBitSizes;

    private bool TryResolveCurve(OkpSecretKey key, [NotNullWhen(true)] out OkpCurveInfo? resolved)
    {
        // See ADR-1390: only a signing curve resolves, so an X25519 or X448 key never signs.
        if (
            OkpCurves.TryGetByName(key.Curve, OkpCurveUse.Signature, out resolved)
            && (Curve is null || resolved == Curve)
        )
        {
            return true;
        }

        resolved = null;
        return false;
    }

    /// <inheritdoc />
    public override bool IsKeyCompatible(SecretKey secretKey) =>
        base.IsKeyCompatible(secretKey) && TryResolveCurve((OkpSecretKey)secretKey, out _);

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
                $"The OKP curve '{okpSecretKey.Curve}' is not supported by the '{Code}' algorithm."
            );
        }

        return OkpCurves.GetTokenHashFunction(resolved, out hashSizeBytes);
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
                $"The OKP curve '{okpSecretKey.Curve}' is not supported by the '{Code}' algorithm."
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
            OkpCurves.Sign(curve, seed, inputData, signature[..curve.SignatureSize]);
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
                $"The OKP curve '{okpSecretKey.Curve}' is not supported by the '{Code}' algorithm."
            );
        }

        if (signature.Length != curve.SignatureSize)
        {
            return false;
        }

        Span<byte> publicKey = stackalloc byte[curve.PublicKeySize];
        var writer = new FixedSpanBufferWriter<byte>(publicKey);
        okpSecretKey.ExportPublicKey(ref writer);

        return OkpCurves.Verify(curve, publicKey, inputData, signature);
    }
}
