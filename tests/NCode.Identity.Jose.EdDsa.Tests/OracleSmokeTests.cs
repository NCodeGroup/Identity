// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;
using Xunit;

namespace NCode.Identity.Jose.EdDsa.Tests;

// Confirms the test-only differential oracle is available and functional before it is used to cross-check the
// clean-room RFC 8032 implementation.
public class OracleSmokeTests
{
    [Fact]
    public void Oracle_Ed25519_SignVerify_RoundTrips()
    {
        var privateKey = new Ed25519PrivateKeyParameters(new SecureRandom());
        var publicKey = privateKey.GeneratePublicKey();
        var message = "hello eddsa"u8.ToArray();

        var signer = new Ed25519Signer();
        signer.Init(forSigning: true, privateKey);
        signer.BlockUpdate(message, 0, message.Length);
        var signature = signer.GenerateSignature();

        var verifier = new Ed25519Signer();
        verifier.Init(forSigning: false, publicKey);
        verifier.BlockUpdate(message, 0, message.Length);

        Assert.True(verifier.VerifySignature(signature));
    }
}
