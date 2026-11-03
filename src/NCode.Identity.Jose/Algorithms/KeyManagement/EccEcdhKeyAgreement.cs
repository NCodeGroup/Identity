// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Security.Cryptography;
using NCode.Identity.Jose.Exceptions;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;

namespace NCode.Identity.Jose.Algorithms.KeyManagement;

/// <summary>
/// Provides the <see cref="IEcdhKeyAgreement"/> of the <c>ECDH-ES</c> algorithms over <c>EC</c> keys on the
/// <c>P-256</c>, <c>P-384</c> and <c>P-521</c> curves (RFC 7518 §4.6). The shared secret <c>Z</c> is the
/// x-coordinate of the agreed point, of the curve's field size (NIST SP 800-56A §5.7.1.2). An ephemeral public key is
/// validated to lie on the recipient's curve when it is imported.
/// </summary>
// See ADR-1389.
internal sealed class EccEcdhKeyAgreement : IEcdhKeyAgreement
{
    private static ECCurve[] SupportedCurves { get; } =
    [ECCurve.NamedCurves.nistP256, ECCurve.NamedCurves.nistP384, ECCurve.NamedCurves.nistP521];

    /// <inheritdoc />
    public AlgorithmKeyType KeyType { get; } =
        new(
            typeof(EccSecretKey),
            [new KeySizes(minSize: 256, maxSize: 384, skipSize: 128), new KeySizes(521, 521, 0)]
        )
        {
            Curves = [new("P-256", 256), new("P-384", 384), new("P-521", 521)],
        };

    /// <inheritdoc />
    public bool IsKeyCompatible(SecretKey secretKey) =>
        secretKey is EccSecretKey eccSecretKey
        && KeyType.IsLegalSize(secretKey.KeySizeBits)
        && IsSupportedCurve(eccSecretKey.GetECCurve());

    /// <inheritdoc />
    public int GetSharedSecretSizeBytes(SecretKey secretKey) => (secretKey.KeySizeBits + 7) >> 3;

    /// <inheritdoc />
    public void AgreeWithEphemeralKey(
        SecretKey recipientKey,
        IBufferWriter<byte> ephemeralPublicKeyWriter,
        Span<byte> sharedSecret
    )
    {
        var eccSecretKey = Validate(recipientKey);

        using var recipient = eccSecretKey.ExportECDiffieHellman();
        using var recipientPublicKey = recipient.PublicKey;
        using var ephemeral = ECDiffieHellman.Create(eccSecretKey.GetECCurve());

        CopySharedSecret(ephemeral.DeriveRawSecretAgreement(recipientPublicKey), sharedSecret);

        // RFC 7518 §4.6.1.1: only the ephemeral public key leaves this method.
        ephemeralPublicKeyWriter.Write(ephemeral.ExportSubjectPublicKeyInfo());
    }

    /// <inheritdoc />
    public void Agree(SecretKey recipientKey, SecretKey ephemeralPublicKey, Span<byte> sharedSecret)
    {
        var eccSecretKey = Validate(recipientKey);

        if (
            ephemeralPublicKey is not EccSecretKey ephemeralEccSecretKey
            || !EccCurveSpecification.IsSameNamedCurve(
                ephemeralEccSecretKey.GetECCurve(),
                eccSecretKey.GetECCurve()
            )
        )
        {
            throw new JoseException(
                "The ephemeral public key is not on the curve of the recipient's key."
            );
        }

        using var recipient = eccSecretKey.ExportECDiffieHellman();
        using var ephemeral = ephemeralEccSecretKey.ExportECDiffieHellman();
        using var ephemeralPublic = ephemeral.PublicKey;

        CopySharedSecret(recipient.DeriveRawSecretAgreement(ephemeralPublic), sharedSecret);
    }

    private static bool IsSupportedCurve(ECCurve curve) =>
        SupportedCurves.Any(supported => EccCurveSpecification.IsSameNamedCurve(curve, supported));

    private EccSecretKey Validate(SecretKey secretKey)
    {
        var eccSecretKey = secretKey.Validate<EccSecretKey>(KeyType.KeyBitSizes);

        if (!IsSupportedCurve(eccSecretKey.GetECCurve()))
        {
            // the argument that is invalid is the caller's key, not this helper's parameter
#pragma warning disable CA2208
            throw new ArgumentException(
                "The secret key's elliptic curve is not valid for the ECDH-ES algorithms.",
                "recipientKey"
            );
#pragma warning restore CA2208
        }

        return eccSecretKey;
    }

    private static void CopySharedSecret(byte[] secret, Span<byte> sharedSecret)
    {
        try
        {
            if (secret.Length != sharedSecret.Length)
            {
                throw new CryptographicException(
                    "The shared secret does not have the size of the curve's field."
                );
            }

            secret.CopyTo(sharedSecret);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }
}
