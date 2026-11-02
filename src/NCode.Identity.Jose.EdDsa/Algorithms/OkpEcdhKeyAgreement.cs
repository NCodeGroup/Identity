// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using NCode.Buffers;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.EdDsa.Keys;
using NCode.Identity.Jose.EdDsa.KeyTypes;
using NCode.Identity.Jose.Exceptions;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.EdDsa.Algorithms;

/// <summary>
/// Provides the <see cref="IEcdhKeyAgreement"/> of the <c>ECDH-ES</c> algorithms over <c>OKP</c> keys on the X25519
/// and X448 curves (RFC 8037 §3.2). The shared secret <c>Z</c> is the output of the X25519 or X448 function
/// (RFC 7748 §6), of 32 or 56 bytes. An agreement whose shared secret is all zeros, which a public key of small order
/// produces, is refused on both sides. An <c>OKP</c> key on a signing curve (Ed25519, Ed448) never takes part.
/// </summary>
// See ADR-1390.
internal sealed class OkpEcdhKeyAgreement : IEcdhKeyAgreement
{
    /// <inheritdoc />
    public AlgorithmKeyType KeyType { get; } =
        new(
            typeof(OkpSecretKey),
            [
                new KeySizes(
                    minSize: OkpCurves.X25519KeySizeBits,
                    maxSize: OkpCurves.X25519KeySizeBits,
                    skipSize: 0
                ),
                new KeySizes(
                    minSize: OkpCurves.X448KeySizeBits,
                    maxSize: OkpCurves.X448KeySizeBits,
                    skipSize: 0
                ),
            ]
        );

    /// <inheritdoc />
    public bool IsKeyCompatible(SecretKey secretKey) =>
        secretKey is OkpSecretKey okpSecretKey
        && KeyType.IsLegalSize(secretKey.KeySizeBits)
        && TryResolveCurve(okpSecretKey, out _);

    /// <inheritdoc />
    public int GetSharedSecretSizeBytes(SecretKey secretKey) => Validate(secretKey).PublicKeySize;

    /// <inheritdoc />
    public void AgreeWithEphemeralKey(
        SecretKey recipientKey,
        IBufferWriter<byte> ephemeralPublicKeyWriter,
        Span<byte> sharedSecret
    )
    {
        var curve = Validate(recipientKey);
        ValidateSharedSecretSize(curve, sharedSecret);

        Span<byte> recipientPublicKey = stackalloc byte[curve.PublicKeySize];
        var recipientPublicKeyWriter = new FixedSpanBufferWriter<byte>(recipientPublicKey);
        ((OkpSecretKey)recipientKey).ExportPublicKey(ref recipientPublicKeyWriter);

        Span<byte> ephemeralPrivateKey = stackalloc byte[curve.PrivateKeySize];
        Span<byte> ephemeralPublicKey = stackalloc byte[curve.PublicKeySize];
        try
        {
            RandomNumberGenerator.Fill(ephemeralPrivateKey);
            OkpCurves.GeneratePublicKey(curve, ephemeralPrivateKey, ephemeralPublicKey);

            // RFC 7748 §6: a recipient key of small order makes every shared secret all zeros, so the content would be
            // encrypted under a key anyone can derive.
            if (!OkpCurves.TryAgree(curve, ephemeralPrivateKey, recipientPublicKey, sharedSecret))
            {
                CryptographicOperations.ZeroMemory(sharedSecret);
                throw new JoseException(
                    "The recipient's public key is a point of small order, which yields an all-zero shared secret."
                );
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(ephemeralPrivateKey);
        }

        // RFC 7518 §4.6.1.1: only the ephemeral public key leaves this method.
        OkpKeyEncoder.WriteSubjectPublicKeyInfo(
            curve,
            ephemeralPublicKey,
            ref ephemeralPublicKeyWriter
        );
    }

    /// <inheritdoc />
    public void Agree(SecretKey recipientKey, SecretKey ephemeralPublicKey, Span<byte> sharedSecret)
    {
        var curve = Validate(recipientKey);
        ValidateSharedSecretSize(curve, sharedSecret);

        var okpSecretKey = (OkpSecretKey)recipientKey;
        if (!okpSecretKey.HasPrivateKey)
        {
            throw new InvalidOperationException(
                "The OKP secret key holds only the public half and cannot agree keys as a recipient."
            );
        }

        if (
            ephemeralPublicKey is not OkpSecretKey ephemeralOkpSecretKey
            || !string.Equals(ephemeralOkpSecretKey.Curve, curve.Name, StringComparison.Ordinal)
            || ephemeralOkpSecretKey.KeySizeBits != curve.KeySizeBits
        )
        {
            throw new JoseException(
                "The ephemeral public key is not on the curve of the recipient's key."
            );
        }

        Span<byte> peerPublicKey = stackalloc byte[curve.PublicKeySize];
        var peerPublicKeyWriter = new FixedSpanBufferWriter<byte>(peerPublicKey);
        ephemeralOkpSecretKey.ExportPublicKey(ref peerPublicKeyWriter);

        Span<byte> privateKey = stackalloc byte[curve.PrivateKeySize];
        try
        {
            var privateKeyWriter = new FixedSpanBufferWriter<byte>(privateKey);
            okpSecretKey.ExportPrivateKey(ref privateKeyWriter);

            // RFC 7748 §6 and RFC 8037 §4: an ephemeral key of small order yields an all-zero shared secret, which
            // the sender could have chosen to force a known content encryption key.
            if (!OkpCurves.TryAgree(curve, privateKey, peerPublicKey, sharedSecret))
            {
                CryptographicOperations.ZeroMemory(sharedSecret);
                throw new JoseException(
                    "The ephemeral public key is a point of small order, which yields an all-zero shared secret."
                );
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(privateKey);
        }
    }

    private static bool TryResolveCurve(
        OkpSecretKey secretKey,
        [NotNullWhen(true)] out OkpCurveInfo? curve
    ) =>
        OkpCurves.TryGetByName(secretKey.Curve, OkpCurveUse.KeyAgreement, out curve)
        && secretKey.KeySizeBits == curve.KeySizeBits;

    private static void ValidateSharedSecretSize(OkpCurveInfo curve, Span<byte> sharedSecret)
    {
        if (sharedSecret.Length != curve.PublicKeySize)
        {
            throw new ArgumentException(
                $"The shared secret of an {curve.Name} agreement is {curve.PublicKeySize} bytes.",
                nameof(sharedSecret)
            );
        }
    }

    private OkpCurveInfo Validate(SecretKey secretKey)
    {
        var okpSecretKey = secretKey.Validate<OkpSecretKey>(KeyType.KeyBitSizes);

        if (!TryResolveCurve(okpSecretKey, out var curve))
        {
            // the argument that is invalid is the caller's key, not this helper's parameter
#pragma warning disable CA2208
            throw new ArgumentException(
                $"The OKP curve '{okpSecretKey.Curve}' is not valid for the ECDH-ES algorithms; expected "
                    + $"'{OkpCurves.X25519Name}' or '{OkpCurves.X448Name}'.",
                "recipientKey"
            );
#pragma warning restore CA2208
        }

        return curve;
    }
}
