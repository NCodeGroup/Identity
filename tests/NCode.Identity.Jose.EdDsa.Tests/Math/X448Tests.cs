// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Jose.EdDsa.Math;
using Xunit;
using OracleX448 = Org.BouncyCastle.Math.EC.Rfc7748.X448;

namespace NCode.Identity.Jose.EdDsa.Tests.Math;

// Validates the clean-room X448 function against the RFC 7748 §5.2 and §6.2 test vectors, byte for byte against the
// independent oracle over random inputs, and checks the decoding rules and the all-zero shared secret refusal.
public class X448Tests
{
    private static byte[] Hex(string value) => Convert.FromHexString(value);

    private static byte[] ScalarMult(byte[] scalar, byte[] u)
    {
        var output = new byte[X448.SharedSecretSize];
        X448.ScalarMult(scalar, u, output);
        return output;
    }

    #region ScalarMult Tests

    // RFC 7748 §5.2.
    [Theory]
    [InlineData(
        "3d262fddf9ec8e88495266fea19a34d28882acef045104d0d1aae121700a779c984c24f8cdd78fbff44943eba368f54b29259a4f1c600ad3",
        "06fce640fa3487bfda5f6cf2d5263f8aad88334cbd07437f020f08f9814dc031ddbdc38c19c6da2583fa5429db94ada18aa7a7fb4ef8a086",
        "ce3e4ff95a60dc6697da1db1d85e6afbdf79b50a2412d7546d5f239fe14fbaadeb445fc66a01b0779d98223961111e21766282f73dd96b6f"
    )]
    [InlineData(
        "203d494428b8399352665ddca42f9de8fef600908e0d461cb021f8c538345dd77c3e4806e25f46d3315c44e0a5b4371282dd2c8d5be3095f",
        "0fbcc2f993cd56d3305b0b7d9e55d4c1a8fb5dbb52f8e9a1e9b6201b165d015894e56c4d3570bee52fe205e28a78b91cdfbde71ce8d157db",
        "884a02576239ff7a2f2f63b2db6a9ff37047ac13568e1e30fe63c4a7ad1b3ee3a5700df34321d62077e63633c575c1c954514e99da7c179d"
    )]
    public void ScalarMult_WhenRfc7748Vector_MatchesExpected(
        string scalar,
        string u,
        string expected
    )
    {
        Assert.Equal(Hex(expected), ScalarMult(Hex(scalar), Hex(u)));
    }

    // RFC 7748 §5.2: k = u = 5, then k = X448(k, u) and u = the old k, repeated.
    [Theory]
    [InlineData(
        1,
        "3f482c8a9f19b01e6c46ee9711d9dc14fd4bf67af30765c2ae2b846a4d23a8cd0db897086239492caf350b51f833868b9bc2b3bca9cf4113"
    )]
    [InlineData(
        1000,
        "aa3b4749d55b9daf1e5b00288826c467274ce3ebbdd5c17b975e09d4af6c67cf10d087202db88286e2b79fceea3ec353ef54faa26e219f38"
    )]
    public void ScalarMult_WhenIterated_MatchesRfc7748(int iterations, string expected)
    {
        var k = new byte[56];
        k[0] = 5;
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
    public void ScalarMult_WhenUIsNonCanonical_ReducesIt()
    {
        var scalar = Hex(
            "3d262fddf9ec8e88495266fea19a34d28882acef045104d0d1aae121700a779c984c24f8cdd78fbff44943eba368f54b29259a4f1c600ad3"
        );
        var five = new byte[56];
        five[0] = 5;
        // p + 5, the non-canonical encoding of 5.
        var nonCanonicalFive = Hex(
            "04000000000000000000000000000000000000000000000000000000ffffffffffffffffffffffffffffffffffffffffffffffffffffffff"
        );

        Assert.Equal(
            Hex(
                "078dc8e73158e3a63345f6729d0a386435b4d7ad2e033aa413985a60b443956007427dd89e81a36dc0db81752cc338824369985b4ae58c7d"
            ),
            ScalarMult(scalar, nonCanonicalFive)
        );
        Assert.Equal(ScalarMult(scalar, five), ScalarMult(scalar, nonCanonicalFive));
    }

    [Fact]
    public void ScalarMult_WhenRandomInputs_MatchesOracle()
    {
        var random = new Random(7748);
        var scalar = new byte[56];
        var u = new byte[56];
        for (var i = 0; i < 100; i++)
        {
            random.NextBytes(scalar);
            random.NextBytes(u);

            var expected = new byte[56];
            OracleX448.ScalarMult(scalar, 0, u, 0, expected, 0);

            Assert.Equal(expected, ScalarMult(scalar, u));
        }
    }

    [Fact]
    public void ScalarMult_WhenUHasWrongLength_Throws()
    {
        Assert.Throws<ArgumentException>(() => ScalarMult(new byte[56], new byte[57]));
    }

    #endregion

    #region GeneratePublicKey Tests

    // RFC 7748 §6.2.
    [Theory]
    [InlineData(
        "9a8f4925d1519f5775cf46b04b5800d4ee9ee8bae8bc5565d498c28dd9c9baf574a9419744897391006382a6f127ab1d9ac2d8c0a598726b",
        "9b08f7cc31b7e3e67d22d5aea121074a273bd2b83de09c63faa73d2c22c5d9bbc836647241d953d40c5b12da88120d53177f80e532c41fa0"
    )]
    [InlineData(
        "1c306a7ac2a0e2e0990b294470cba339e6453772b075811d8fad0d1d6927c120bb5ee8972b0d3e21374c9c921b09d1b0366f10b65173992d",
        "3eb7a829b0cd20f5bcfc0b599b6feccf6da4627107bdb0d4f345b43027d8b972fc3e34fb4232a13ca706dcb57aec3dae07bdc1c67bf33609"
    )]
    public void GeneratePublicKey_WhenRfc7748Key_MatchesExpected(string privateKey, string expected)
    {
        var publicKey = new byte[56];

        X448.GeneratePublicKey(Hex(privateKey), publicKey);

        Assert.Equal(Hex(expected), publicKey);
    }

    #endregion

    #region TryAgree Tests

    // RFC 7748 §6.2: Alice and Bob agree on the same shared secret.
    [Theory]
    [InlineData(
        "9a8f4925d1519f5775cf46b04b5800d4ee9ee8bae8bc5565d498c28dd9c9baf574a9419744897391006382a6f127ab1d9ac2d8c0a598726b",
        "3eb7a829b0cd20f5bcfc0b599b6feccf6da4627107bdb0d4f345b43027d8b972fc3e34fb4232a13ca706dcb57aec3dae07bdc1c67bf33609"
    )]
    [InlineData(
        "1c306a7ac2a0e2e0990b294470cba339e6453772b075811d8fad0d1d6927c120bb5ee8972b0d3e21374c9c921b09d1b0366f10b65173992d",
        "9b08f7cc31b7e3e67d22d5aea121074a273bd2b83de09c63faa73d2c22c5d9bbc836647241d953d40c5b12da88120d53177f80e532c41fa0"
    )]
    public void TryAgree_WhenRfc7748Keys_ReturnsTheSharedSecret(
        string privateKey,
        string peerPublicKey
    )
    {
        var sharedSecret = new byte[56];

        var result = X448.TryAgree(Hex(privateKey), Hex(peerPublicKey), sharedSecret);

        Assert.True(result);
        Assert.Equal(
            Hex(
                "07fff4181ac6cc95ec1c16a94a0f74d12da232ce40a77552281d282bb60c0b56fd2464c335543936521c24403085d59a449a5037514a879d"
            ),
            sharedSecret
        );
    }

    // The points of small order: 0, 1 and p - 1.
    [Theory]
    [InlineData(
        "0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000"
    )]
    [InlineData(
        "0100000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000"
    )]
    [InlineData(
        "fefffffffffffffffffffffffffffffffffffffffffffffffffffffffeffffffffffffffffffffffffffffffffffffffffffffffffffffff"
    )]
    public void TryAgree_WhenPeerKeyHasSmallOrder_ReturnsFalse(string peerPublicKey)
    {
        var sharedSecret = new byte[56];

        var result = X448.TryAgree(
            Hex(
                "9a8f4925d1519f5775cf46b04b5800d4ee9ee8bae8bc5565d498c28dd9c9baf574a9419744897391006382a6f127ab1d9ac2d8c0a598726b"
            ),
            Hex(peerPublicKey),
            sharedSecret
        );

        Assert.False(result);
        Assert.Equal(new byte[56], sharedSecret);
    }

    #endregion
}
