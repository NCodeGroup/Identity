#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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
