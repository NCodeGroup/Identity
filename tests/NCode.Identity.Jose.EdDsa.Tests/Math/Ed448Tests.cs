#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using NCode.Identity.Jose.EdDsa.Math;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Xunit;

namespace NCode.Identity.Jose.EdDsa.Tests.Math;

// Validates the clean-room Ed448 implementation byte-for-byte against the independent oracle (public keys and the
// deterministic signatures must match exactly), plus cross-verification and tamper rejection.
public class Ed448Tests
{
    private static byte[] EmptyContext => [];

    [Fact]
    public void PublicKey_And_Signature_MatchOracle_ByteExact()
    {
        var random = new Random(448909);
        var seed = new byte[57];
        for (var i = 0; i < 50; i++)
        {
            random.NextBytes(seed);
            var message = new byte[random.Next(0, 300)];
            random.NextBytes(message);

            var myPublicKey = new byte[57];
            Ed448.GeneratePublicKey(seed, myPublicKey);
            var mySignature = new byte[114];
            Ed448.Sign(seed, message, mySignature);

            var oraclePrivate = new Ed448PrivateKeyParameters(seed, 0);
            var oraclePublic = oraclePrivate.GeneratePublicKey().GetEncoded();
            var signer = new Ed448Signer(EmptyContext);
            signer.Init(forSigning: true, oraclePrivate);
            signer.BlockUpdate(message, 0, message.Length);
            var oracleSignature = signer.GenerateSignature();

            Assert.Equal(oraclePublic, myPublicKey);
            Assert.Equal(oracleSignature, mySignature);

            Assert.True(Ed448.Verify(myPublicKey, message, oracleSignature));

            var verifier = new Ed448Signer(EmptyContext);
            verifier.Init(forSigning: false, new Ed448PublicKeyParameters(myPublicKey, 0));
            verifier.BlockUpdate(message, 0, message.Length);
            Assert.True(verifier.VerifySignature(mySignature));
        }
    }

    [Fact]
    public void Verify_RejectsTampering()
    {
        var seed = new byte[57];
        new Random(13).NextBytes(seed);
        var message = "ed448 round trip"u8.ToArray();

        var publicKey = new byte[57];
        Ed448.GeneratePublicKey(seed, publicKey);
        var signature = new byte[114];
        Ed448.Sign(seed, message, signature);

        Assert.True(Ed448.Verify(publicKey, message, signature));

        var tampered = (byte[])signature.Clone();
        tampered[0] ^= 0x01; // R
        Assert.False(Ed448.Verify(publicKey, message, tampered));

        tampered = (byte[])signature.Clone();
        tampered[60] ^= 0x01; // S
        Assert.False(Ed448.Verify(publicKey, message, tampered));

        Assert.False(Ed448.Verify(publicKey, "ed448 round tripX"u8.ToArray(), signature));
    }
}
