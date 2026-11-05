// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using NCode.Identity.Jose.Algorithms.KeyManagement;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;
using JoseException = NCode.Identity.Jose.Exceptions.JoseException;

namespace NCode.Identity.Jose.Tests.Algorithms.KeyManagement;

/// <summary>
/// Calls the <c>EC</c> agreement of the <c>ECDH-ES</c> algorithms directly, as the algorithm does once it has chosen
/// the agreement, so its own refusals are reached: a key on a curve it does not serve, an ephemeral key of another
/// type, and a shared-secret buffer that is not the curve's field size.
/// </summary>
// See ADR-1389.
public class EccEcdhKeyAgreementTests : BaseTests
{
    private const string SharedSecretSizeMessage =
        "The shared secret does not have the size of the curve's field.";

    private const string UnsupportedCurveMessage =
        "The secret key's elliptic curve is not valid for the ECDH-ES algorithms. (Parameter 'recipientKey')";

    private static EccCurveSpecification BrainpoolCurve { get; } =
        new()
        {
            CurveName = "brainpoolP256r1",
            CurveSizeBits = 256,
            Curve = ECCurve.NamedCurves.brainpoolP256r1,
            Domain = EccCurveDomain.FromCurve(ECCurve.NamedCurves.brainpoolP256r1),
        };

    private EccEcdhKeyAgreement KeyAgreement { get; } = new();

    #region Scaffolding

    private static EccSecretKey CreateKey(ECCurve curve)
    {
        using var key = ECDiffieHellman.Create(curve);
        return SecretKeyFactory.CreateEcc(new KeyMetadata { KeyId = "recipient" }, key);
    }

    // The ephemeral public key as the algorithm reads it from the epk: the public half only.
    private static EccSecretKey CreatePublicKey(ReadOnlySpan<byte> subjectPublicKeyInfo) =>
        SecretKeyFactory.CreateEccSpki(
            new KeyMetadata { KeyId = "ephemeral" },
            subjectPublicKeyInfo
        );

    // A key on a curve the application registers in the elliptic-curve catalog; it has the size of P-256, so only its
    // curve can refuse it.
    private static EccSecretKey CreateBrainpoolKey()
    {
        var factory = new DefaultSecretKeyFactory(
            new DefaultSecretKeyDataProtectorFactory(NullLoggerFactory.Instance),
            new DefaultEccCurveCatalog([BrainpoolCurve])
        );
        using var key = ECDiffieHellman.Create(BrainpoolCurve.Curve);
        return factory.CreateEcc(new KeyMetadata { KeyId = "brainpool" }, key);
    }

    #endregion

    #region GetSharedSecretSizeBytes Tests

    // NIST SP 800-56A §5.7.1.2: the shared secret is the x-coordinate, of the curve's field size.
    [Theory]
    [InlineData(256, 32)]
    [InlineData(384, 48)]
    [InlineData(521, 66)]
    public void GetSharedSecretSizeBytes_WhenNistCurve_ReturnsTheFieldSize(
        int curveSizeBits,
        int expected
    )
    {
        var curve = curveSizeBits switch
        {
            256 => ECCurve.NamedCurves.nistP256,
            384 => ECCurve.NamedCurves.nistP384,
            _ => ECCurve.NamedCurves.nistP521,
        };

        Assert.Equal(expected, KeyAgreement.GetSharedSecretSizeBytes(CreateKey(curve)));
    }

    #endregion

    #region AgreeWithEphemeralKey Tests

    [Fact]
    public void AgreeWithEphemeralKey_WhenSharedSecretIsTooSmall_ThrowsAndWritesNoEphemeralKey()
    {
        var ephemeralPublicKey = new ArrayBufferWriter<byte>();
        var sharedSecret = new byte[31];

        var exception = Assert.Throws<CryptographicException>(() =>
            KeyAgreement.AgreeWithEphemeralKey(
                CreateKey(ECCurve.NamedCurves.nistP256),
                ephemeralPublicKey,
                sharedSecret
            )
        );

        Assert.Equal(SharedSecretSizeMessage, exception.Message);
        Assert.Equal(0, ephemeralPublicKey.WrittenCount);
        Assert.All(sharedSecret, value => Assert.Equal(0, value));
    }

    [Fact]
    public void AgreeWithEphemeralKey_WhenRecipientCurveIsNotSupported_Throws()
    {
        var ephemeralPublicKey = new ArrayBufferWriter<byte>();

        var exception = Assert.Throws<ArgumentException>(() =>
            KeyAgreement.AgreeWithEphemeralKey(
                CreateBrainpoolKey(),
                ephemeralPublicKey,
                new byte[32]
            )
        );

        Assert.Equal("recipientKey", exception.ParamName);
        Assert.Equal(UnsupportedCurveMessage, exception.Message);
        Assert.Equal(0, ephemeralPublicKey.WrittenCount);
    }

    #endregion

    #region Agree Tests

    // The control for the refusals below: the recipient agrees on the sender's shared secret.
    [Fact]
    public void Agree_WhenTheSenderAgreed_ReturnsTheSameSharedSecret()
    {
        var recipientKey = CreateKey(ECCurve.NamedCurves.nistP256);
        var ephemeralPublicKey = new ArrayBufferWriter<byte>();
        var senderSecret = new byte[32];
        KeyAgreement.AgreeWithEphemeralKey(recipientKey, ephemeralPublicKey, senderSecret);
        var recipientSecret = new byte[32];

        KeyAgreement.Agree(
            recipientKey,
            CreatePublicKey(ephemeralPublicKey.WrittenSpan),
            recipientSecret
        );

        Assert.Equal(senderSecret, recipientSecret);
    }

    [Fact]
    public void Agree_WhenEphemeralKeyIsNotAnEccKey_Throws()
    {
        using var rsa = RSA.Create(2048);
        var ephemeralKey = SecretKeyFactory.CreateRsa(new KeyMetadata { KeyId = "rsa" }, rsa);

        var exception = Assert.Throws<JoseException>(() =>
            KeyAgreement.Agree(CreateKey(ECCurve.NamedCurves.nistP256), ephemeralKey, new byte[32])
        );

        Assert.Equal(
            "The ephemeral public key is not on the curve of the recipient's key.",
            exception.Message
        );
    }

    [Fact]
    public void Agree_WhenSharedSecretIsTooSmall_Throws()
    {
        var recipientKey = CreateKey(ECCurve.NamedCurves.nistP256);
        var ephemeralPublicKey = new ArrayBufferWriter<byte>();
        KeyAgreement.AgreeWithEphemeralKey(recipientKey, ephemeralPublicKey, new byte[32]);
        var sharedSecret = new byte[31];

        var exception = Assert.Throws<CryptographicException>(() =>
            KeyAgreement.Agree(
                recipientKey,
                CreatePublicKey(ephemeralPublicKey.WrittenSpan),
                sharedSecret
            )
        );

        Assert.Equal(SharedSecretSizeMessage, exception.Message);
        Assert.All(sharedSecret, value => Assert.Equal(0, value));
    }

    [Fact]
    public void Agree_WhenRecipientCurveIsNotSupported_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            KeyAgreement.Agree(
                CreateBrainpoolKey(),
                CreateKey(ECCurve.NamedCurves.nistP256),
                new byte[32]
            )
        );

        Assert.Equal("recipientKey", exception.ParamName);
        Assert.Equal(UnsupportedCurveMessage, exception.Message);
    }

    #endregion
}
