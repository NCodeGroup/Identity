// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Algorithms.Signature;
using NCode.Identity.Jose.Credentials;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.Tests.Credentials;

public class DefaultCredentialSelectorTests : BaseTests
{
    private static EccSecretKey CreateKey(ECCurve curve, string keyId)
    {
        var metadata = new KeyMetadata { KeyId = keyId };
        using var key = ECDsa.Create(curve);
        return SecretKeyFactory.CreateEcc(metadata, key);
    }

    [Fact]
    public void TryGetSigningCredentials_WhenFirstKeyHasWrongCurve_SelectsMatchingCurveKey()
    {
        var collection = new DefaultAlgorithmCollection([
            new EccSignatureAlgorithm(
                AlgorithmCodes.DigitalSignature.EcdsaSha256,
                ECCurve.NamedCurves.nistP256,
                HashAlgorithmName.SHA256
            ),
        ]);
        var wrongCurveKey = CreateKey(ECCurve.NamedCurves.brainpoolP256r1, "brainpool");
        var p256Key = CreateKey(ECCurve.NamedCurves.nistP256, "p256");

        var selector = new DefaultCredentialSelector();
        var result = selector.TryGetSigningCredentials(
            collection,
            ["ES256"],
            [wrongCurveKey, p256Key],
            out var credentials
        );

        Assert.True(result);
        Assert.NotNull(credentials);
        Assert.Same(p256Key, credentials.SecretKey);
    }
}
