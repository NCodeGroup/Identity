// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Formats.Asn1;
using System.Security.Cryptography;

namespace NCode.Identity.Secrets.RemoteKeys;

// See ADR-1471.
public class EcdsaSignatureConverterTests
{
    private static byte[] BuildDer(byte[] r, byte[] s)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteIntegerUnsigned(r);
            writer.WriteIntegerUnsigned(s);
        }
        return writer.Encode();
    }

    private static byte[] Filled(int length, byte value) =>
        new byte[length]
            .Select(_ => value)
            .ToArray();

    #region ConvertDerToIeeeP1363 Tests

    [Theory]
    [InlineData(256, 32)]
    [InlineData(384, 48)]
    [InlineData(521, 66)]
    public void ConvertDerToIeeeP1363_WhenSignedByEcdsa_ProducesSignatureThatP1363Verifies(
        int keySizeBits,
        int fieldSizeBytes
    )
    {
        var (curve, hashAlgorithm) = keySizeBits switch
        {
            256 => (ECCurve.NamedCurves.nistP256, HashAlgorithmName.SHA256),
            384 => (ECCurve.NamedCurves.nistP384, HashAlgorithmName.SHA384),
            _ => (ECCurve.NamedCurves.nistP521, HashAlgorithmName.SHA512),
        };
        using var ecdsa = ECDsa.Create(curve);
        var data = "signing input"u8.ToArray();

        for (var i = 0; i < 16; ++i)
        {
            var der = ecdsa.SignData(data, hashAlgorithm, DSASignatureFormat.Rfc3279DerSequence);
            var destination = new byte[fieldSizeBytes * 2];

            var written = EcdsaSignatureConverter.ConvertDerToIeeeP1363(
                der,
                fieldSizeBytes,
                destination
            );

            Assert.Equal(destination.Length, written);
            Assert.True(
                ecdsa.VerifyData(
                    data,
                    destination,
                    hashAlgorithm,
                    DSASignatureFormat.IeeeP1363FixedFieldConcatenation
                )
            );
        }
    }

    [Fact]
    public void ConvertDerToIeeeP1363_WhenIntegersAreShort_LeftPadsWithZeros()
    {
        var der = BuildDer([0x01], [0x02, 0x03]);
        var destination = Filled(64, 0xFF);

        var written = EcdsaSignatureConverter.ConvertDerToIeeeP1363(der, 32, destination);

        var expected = new byte[64];
        expected[31] = 0x01;
        expected[62] = 0x02;
        expected[63] = 0x03;
        Assert.Equal(64, written);
        Assert.Equal(expected, destination);
    }

    [Fact]
    public void ConvertDerToIeeeP1363_WhenIntegerHasLeadingZeroSignByte_DropsTheSignByte()
    {
        var r = Filled(32, 0x80);
        var der = BuildDer(r, [0x01]);
        Assert.Equal(0x00, der[4]);
        var destination = new byte[64];

        EcdsaSignatureConverter.ConvertDerToIeeeP1363(der, 32, destination);

        Assert.Equal(r, destination[..32]);
        Assert.Equal(0x01, destination[63]);
    }

    [Fact]
    public void ConvertDerToIeeeP1363_WhenDestinationIsLarger_WritesTheFieldsOnly()
    {
        var der = BuildDer([0x01], [0x02]);
        var destination = Filled(70, 0xFF);

        var written = EcdsaSignatureConverter.ConvertDerToIeeeP1363(der, 32, destination);

        Assert.Equal(64, written);
        Assert.Equal(Filled(6, 0xFF), destination[64..]);
    }

    [Fact]
    public void ConvertDerToIeeeP1363_WhenIntegerIsNegative_Throws()
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteInteger([0xFF]);
            writer.WriteIntegerUnsigned([0x01]);
        }
        var der = writer.Encode();

        Assert.Throws<CryptographicException>(() =>
            EcdsaSignatureConverter.ConvertDerToIeeeP1363(der, 32, new byte[64])
        );
    }

    [Fact]
    public void ConvertDerToIeeeP1363_WhenIntegerExceedsTheField_Throws()
    {
        var der = BuildDer([0x01, .. Filled(32, 0x02)], [0x01]);

        Assert.Throws<CryptographicException>(() =>
            EcdsaSignatureConverter.ConvertDerToIeeeP1363(der, 32, new byte[64])
        );
    }

    [Fact]
    public void ConvertDerToIeeeP1363_WhenDataFollowsTheSequence_Throws()
    {
        byte[] der = [.. BuildDer([0x01], [0x02]), 0x00];

        Assert.Throws<CryptographicException>(() =>
            EcdsaSignatureConverter.ConvertDerToIeeeP1363(der, 32, new byte[64])
        );
    }

    [Fact]
    public void ConvertDerToIeeeP1363_WhenSequenceHoldsAThirdInteger_Throws()
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteIntegerUnsigned([0x01]);
            writer.WriteIntegerUnsigned([0x02]);
            writer.WriteIntegerUnsigned([0x03]);
        }
        var der = writer.Encode();

        Assert.Throws<CryptographicException>(() =>
            EcdsaSignatureConverter.ConvertDerToIeeeP1363(der, 32, new byte[64])
        );
    }

    [Fact]
    public void ConvertDerToIeeeP1363_WhenInputIsNotASequence_Throws()
    {
        byte[] der = [0x02, 0x01, 0x01];

        Assert.Throws<CryptographicException>(() =>
            EcdsaSignatureConverter.ConvertDerToIeeeP1363(der, 32, new byte[64])
        );
    }

    [Fact]
    public void ConvertDerToIeeeP1363_WhenDestinationIsShort_Throws()
    {
        var der = BuildDer([0x01], [0x02]);

        Assert.Throws<CryptographicException>(() =>
            EcdsaSignatureConverter.ConvertDerToIeeeP1363(der, 32, new byte[63])
        );
    }

    #endregion
}
