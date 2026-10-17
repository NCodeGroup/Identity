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

// Validates the clean-room Ed25519 implementation byte-for-byte against the independent oracle (public keys and the
// deterministic signatures must match exactly), plus cross-verification and tamper rejection.
public class Ed25519Tests
{
    [Fact]
    public void PublicKey_And_Signature_MatchOracle_ByteExact()
    {
        var random = new Random(2024);
        var seed = new byte[32];
        for (var i = 0; i < 100; i++)
        {
            random.NextBytes(seed);
            var message = new byte[random.Next(0, 300)];
            random.NextBytes(message);

            var myPublicKey = new byte[32];
            Ed25519.GeneratePublicKey(seed, myPublicKey);
            var mySignature = new byte[64];
            Ed25519.Sign(seed, message, mySignature);

            var oraclePrivate = new Ed25519PrivateKeyParameters(seed, 0);
            var oraclePublic = oraclePrivate.GeneratePublicKey().GetEncoded();
            var signer = new Ed25519Signer();
            signer.Init(forSigning: true, oraclePrivate);
            signer.BlockUpdate(message, 0, message.Length);
            var oracleSignature = signer.GenerateSignature();

            Assert.Equal(oraclePublic, myPublicKey);
            Assert.Equal(oracleSignature, mySignature);

            // Our verifier accepts the oracle's signature, and the oracle accepts ours.
            Assert.True(Ed25519.Verify(myPublicKey, message, oracleSignature));

            var verifier = new Ed25519Signer();
            verifier.Init(forSigning: false, new Ed25519PublicKeyParameters(myPublicKey, 0));
            verifier.BlockUpdate(message, 0, message.Length);
            Assert.True(verifier.VerifySignature(mySignature));
        }
    }

    [Fact]
    public void Verify_RejectsTampering()
    {
        var seed = new byte[32];
        new Random(7).NextBytes(seed);
        var message = "eddsa round trip"u8.ToArray();

        var publicKey = new byte[32];
        Ed25519.GeneratePublicKey(seed, publicKey);
        var signature = new byte[64];
        Ed25519.Sign(seed, message, signature);

        Assert.True(Ed25519.Verify(publicKey, message, signature));

        // Tampered R.
        var tampered = (byte[])signature.Clone();
        tampered[0] ^= 0x01;
        Assert.False(Ed25519.Verify(publicKey, message, tampered));

        // Tampered S.
        tampered = (byte[])signature.Clone();
        tampered[40] ^= 0x01;
        Assert.False(Ed25519.Verify(publicKey, message, tampered));

        // Different message.
        Assert.False(Ed25519.Verify(publicKey, "eddsa round tripX"u8.ToArray(), signature));

        // Non-canonical S (S + L) is rejected.
        Assert.False(Ed25519.Verify(publicKey, message, signature.AsSpan().ToArray().AlsoAddL()));
    }
}

file static class SignatureExtensions
{
    // Returns a copy with L added to the S component, making it non-canonical (used to assert rejection).
    public static byte[] AlsoAddL(this byte[] signature)
    {
        // L little-endian.
        ReadOnlySpan<byte> l =
        [
            0xED,
            0xD3,
            0xF5,
            0x5C,
            0x1A,
            0x63,
            0x12,
            0x58,
            0xD6,
            0x9C,
            0xF7,
            0xA2,
            0xDE,
            0xF9,
            0xDE,
            0x14,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x10,
        ];

        var result = (byte[])signature.Clone();
        var s = result.AsSpan(32, 32);
        var carry = 0;
        for (var i = 0; i < 32; i++)
        {
            var sum = s[i] + l[i] + carry;
            s[i] = (byte)sum;
            carry = sum >> 8;
        }

        return result;
    }
}
