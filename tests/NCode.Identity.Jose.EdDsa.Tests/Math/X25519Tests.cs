// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Jose.EdDsa.Math;
using Xunit;
using OracleX25519 = Org.BouncyCastle.Math.EC.Rfc7748.X25519;

namespace NCode.Identity.Jose.EdDsa.Tests.Math;

// Validates the clean-room X25519 function against the RFC 7748 §5.2 and §6.1 test vectors, byte for byte against the
// independent oracle over random inputs, and checks the decoding rules and the all-zero shared secret refusal.
public class X25519Tests
{
    private static byte[] Hex(string value) => Convert.FromHexString(value);

    private static byte[] ScalarMult(byte[] scalar, byte[] u)
    {
        var output = new byte[X25519.SharedSecretSize];
        X25519.ScalarMult(scalar, u, output);
        return output;
    }

    #region ScalarMult Tests

    // RFC 7748 §5.2. The second u-coordinate has its most significant bit set, which the function masks.
    [Theory]
    [InlineData(
        "a546e36bf0527c9d3b16154b82465edd62144c0ac1fc5a18506a2244ba449ac4",
        "e6db6867583030db3594c1a424b15f7c726624ec26b3353b10a903a6d0ab1c4c",
        "c3da55379de9c6908e94ea4df28d084f32eccf03491c71f754b4075577a28552"
    )]
    [InlineData(
        "4b66e9d4d1b4673c5ad22691957d6af5c11b6421e0ea01d42ca4169e7918ba0d",
        "e5210f12786811d3f4b7959d0538ae2c31dbe7106fc03c3efc4cd549c715a493",
        "95cbde9476e8907d7aade45cb4b873f88b595a68799fa152e6f8f7647aac7957"
    )]
    public void ScalarMult_WhenRfc7748Vector_MatchesExpected(
        string scalar,
        string u,
        string expected
    )
    {
        Assert.Equal(Hex(expected), ScalarMult(Hex(scalar), Hex(u)));
    }

    // RFC 7748 §5.2: k = u = 9, then k = X25519(k, u) and u = the old k, repeated.
    [Theory]
    [InlineData(1, "422c8e7a6227d7bca1350b3e2bb7279f7897b87bb6854b783c60e80311ae3079")]
    [InlineData(1000, "684cf59ba83309552800ef566f2f4d3c1c3887c49360e3875f2eb94d99532c51")]
    public void ScalarMult_WhenIterated_MatchesRfc7748(int iterations, string expected)
    {
        var k = new byte[32];
        k[0] = 9;
        var u = k.ToArray();

        for (var i = 0; i < iterations; i++)
        {
            var result = ScalarMult(k, u);
            u = k;
            k = result;
        }

        Assert.Equal(Hex(expected), k);
    }

    [Fact]
    public void ScalarMult_WhenHighBitOfUIsSet_IgnoresIt()
    {
        var scalar = Hex("a546e36bf0527c9d3b16154b82465edd62144c0ac1fc5a18506a2244ba449ac4");
        var u = Hex("e6db6867583030db3594c1a424b15f7c726624ec26b3353b10a903a6d0ab1c4c");
        var masked = u.ToArray();
        masked[31] ^= 0x80;

        Assert.Equal(ScalarMult(scalar, u), ScalarMult(scalar, masked));
    }

    [Fact]
    public void ScalarMult_WhenUIsNonCanonical_ReducesIt()
    {
        var scalar = Hex("a546e36bf0527c9d3b16154b82465edd62144c0ac1fc5a18506a2244ba449ac4");
        var five = new byte[32];
        five[0] = 5;
        // p + 5, the non-canonical encoding of 5.
        var nonCanonicalFive = Hex(
            "f2ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff7f"
        );

        Assert.Equal(
            Hex("50e87fe55e186ff59bcfb758e02f341b4f62573826bb2d28ec182949a4e9cf1f"),
            ScalarMult(scalar, nonCanonicalFive)
        );
        Assert.Equal(ScalarMult(scalar, five), ScalarMult(scalar, nonCanonicalFive));
    }

    [Fact]
    public void ScalarMult_WhenRandomInputs_MatchesOracle()
    {
        var random = new Random(7748);
        var scalar = new byte[32];
        var u = new byte[32];
        for (var i = 0; i < 200; i++)
        {
            random.NextBytes(scalar);
            random.NextBytes(u);

            var expected = new byte[32];
            OracleX25519.ScalarMult(scalar, 0, u, 0, expected, 0);

            Assert.Equal(expected, ScalarMult(scalar, u));
        }
    }

    [Fact]
    public void ScalarMult_WhenScalarHasWrongLength_Throws()
    {
        Assert.Throws<ArgumentException>(() => ScalarMult(new byte[31], new byte[32]));
    }

    #endregion

    #region GeneratePublicKey Tests

    // RFC 7748 §6.1.
    [Theory]
    [InlineData(
        "77076d0a7318a57d3c16c17251b26645df4c2f87ebc0992ab177fba51db92c2a",
        "8520f0098930a754748b7ddcb43ef75a0dbf3a0d26381af4eba4a98eaa9b4e6a"
    )]
    [InlineData(
        "5dab087e624a8a4b79e17f8b83800ee66f3bb1292618b6fd1c2f8b27ff88e0eb",
        "de9edb7d7b7dc1b4d35b61c2ece435373f8343c85b78674dadfc7e146f882b4f"
    )]
    public void GeneratePublicKey_WhenRfc7748Key_MatchesExpected(string privateKey, string expected)
    {
        var publicKey = new byte[32];

        X25519.GeneratePublicKey(Hex(privateKey), publicKey);

        Assert.Equal(Hex(expected), publicKey);
    }

    #endregion

    #region TryAgree Tests

    // RFC 7748 §6.1: Alice and Bob agree on the same shared secret.
    [Theory]
    [InlineData(
        "77076d0a7318a57d3c16c17251b26645df4c2f87ebc0992ab177fba51db92c2a",
        "de9edb7d7b7dc1b4d35b61c2ece435373f8343c85b78674dadfc7e146f882b4f"
    )]
    [InlineData(
        "5dab087e624a8a4b79e17f8b83800ee66f3bb1292618b6fd1c2f8b27ff88e0eb",
        "8520f0098930a754748b7ddcb43ef75a0dbf3a0d26381af4eba4a98eaa9b4e6a"
    )]
    public void TryAgree_WhenRfc7748Keys_ReturnsTheSharedSecret(
        string privateKey,
        string peerPublicKey
    )
    {
        var sharedSecret = new byte[32];

        var result = X25519.TryAgree(Hex(privateKey), Hex(peerPublicKey), sharedSecret);

        Assert.True(result);
        Assert.Equal(
            Hex("4a5d9d5ba4ce2de1728e3bf480350f25e07e21c947d19e3376f09b3c1e161742"),
            sharedSecret
        );
    }

    // Points of small order, and their non-canonical encodings, give an all-zero output.
    [Theory]
    [InlineData("0000000000000000000000000000000000000000000000000000000000000000")]
    [InlineData("0100000000000000000000000000000000000000000000000000000000000000")]
    [InlineData("e0eb7a7c3b41b8ae1656e3faf19fc46ada098deb9c32b1fd866205165f49b800")]
    [InlineData("5f9c95bca3508c24b1d0b1559c83ef5b04445cc4581c8e86d8224eddd09f1157")]
    [InlineData("ecffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff7f")]
    [InlineData("edffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff7f")]
    [InlineData("eeffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff7f")]
    public void TryAgree_WhenPeerKeyHasSmallOrder_ReturnsFalse(string peerPublicKey)
    {
        var sharedSecret = new byte[32];

        var result = X25519.TryAgree(
            Hex("77076d0a7318a57d3c16c17251b26645df4c2f87ebc0992ab177fba51db92c2a"),
            Hex(peerPublicKey),
            sharedSecret
        );

        Assert.False(result);
        Assert.Equal(new byte[32], sharedSecret);
    }

    #endregion
}
