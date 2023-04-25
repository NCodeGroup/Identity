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

using System.Buffers;
using System.Security.Cryptography;
using NIdentity.OpenId.Cryptography.CryptoProvider.Aes;
using NIdentity.OpenId.Cryptography.CryptoProvider.Ecdh;
using NIdentity.OpenId.Cryptography.CryptoProvider.Ecdh.Descriptors;
using NIdentity.OpenId.Cryptography.CryptoProvider.Ecdh.Parameters;
using NIdentity.OpenId.Cryptography.Keys;
using Xunit;

namespace NIdentity.OpenId.Core.Tests.Cryptography.Ecdh;

public class EcdhKeyWrapWithAesKeyWrapProviderTests : BaseTests
{
    private const string KeyId = nameof(KeyId);

    // the following test parameters where generated externally with a known working ECDH implementation (jose-jwt)

    private const string PlainTextKey = "P+PKDLXjVZBOYAWImddUxJFip8VqqwjSxMAAICQ3YYQ=";
    private const string ExpectedDerivedKey = "/TJ28nmufX1uHt+DvdQbaWfCoMexX8k40ERbho48cbcWptDZ6b2Iqg==";

    private const int KeyBitLength = 256; // the intermediate AES key length, must match algorithm
    private const string AlgorithmCode = AlgorithmCodes.KeyManagement.EcdhEsAes256;

    private static ECCurve Curve { get; } = ECCurve.NamedCurves.nistP256;
    private ECParameters Party1Parameters { get; } = LoadParty1Parameters();
    private ECParameters Party2Parameters { get; } = LoadParty2Parameters();

    private static ECParameters LoadParty1Parameters() => new()
    {
        Curve = Curve,
        Q = new ECPoint // public part
        {
            X = Convert.FromBase64String("3BDv2y0CqT9A28qOhJoSp9K6qNSEaGagF6TLuVtCR5g="),
            Y = Convert.FromBase64String("AkR4kvGNucKbDyHW7d5iD/C37aJML+4V+rxcyeXN0ts=")
        },
        // private part
        D = Convert.FromBase64String("Zw1DgcQ2LAex8SBaceej1yCB6IaSPFfBz05JccmImCo=")
    };

    private static ECParameters LoadParty2Parameters() => new()
    {
        Curve = Curve,
        Q = new ECPoint // public part
        {
            X = Convert.FromBase64String("YZAG4YKtXl/sQW+kTERkV3CTjU4CqUeVAFcROMivNYQ="),
            Y = Convert.FromBase64String("u2iWhH749lKT6YMjkGC5eU26/wfM5PsZNSojgnQOD30=")
        },
        // private part
        D = Convert.FromBase64String("CLNiEczZu1yKLG7iOQv+74oFIoulQw4DRBIAk0RNOoQ=")
    };

    private EcdhKeyWrapWithAesKeyWrapAlgorithmDescriptor AlgorithmDescriptor { get; }

    public EcdhKeyWrapWithAesKeyWrapProviderTests()
    {
        AlgorithmDescriptor = new EcdhKeyWrapWithAesKeyWrapAlgorithmDescriptor(
            AlgorithmCode,
            KeyDerivationFunctionTypes.SP800_56A_CONCAT,
            HashAlgorithmName.SHA256,
            HashSizeBits: 256,
            KeyBitLength);
    }

    [Fact]
    public void WrapKey_WhenParty1ToParty2_ThenValid()
    {
        WrapKey(Party1Parameters, LoadParty2Parameters());
    }

    [Fact]
    public void WrapKey_WhenParty2ToParty1_ThenValid()
    {
        WrapKey(Party2Parameters, Party1Parameters);
    }

    private void WrapKey(ECParameters party1Parameters, ECParameters party2Parameters)
    {
        const string partyUInfo = nameof(partyUInfo);
        const string partyVInfo = nameof(partyVInfo);

        using var party1PrivateKey = ECDiffieHellman.Create(party1Parameters);
        using var party2PrivateKey = ECDiffieHellman.Create(party2Parameters);

        var party1Pkcs8PrivateKey = party1PrivateKey.ExportPkcs8PrivateKey();

        var provider = new EcdhKeyWrapWithAesKeyWrapProvider(
            AesKeyWrap.Default,
            new EccSecretKey(KeyId, KeyBitLength, party1Pkcs8PrivateKey),
            AlgorithmDescriptor);

        var plainTextKey = Convert.FromBase64String(PlainTextKey);
        var parameters = new EcdhEsKeyWrapWithAesKeyWrapParameters(
            plainTextKey,
            party2PrivateKey,
            KeyBitLength,
            partyUInfo,
            partyVInfo);
        var derivedKey = provider.WrapKey(parameters);

        Assert.Equal(ExpectedDerivedKey, Convert.ToBase64String(derivedKey.ToArray()));
    }

    [Fact]
    public void RoundTrip_Valid()
    {
        const int anyKeySize = 312;
        var expectedPlainTextKey = GC.AllocateUninitializedArray<byte>(anyKeySize);
        Random.Shared.NextBytes(expectedPlainTextKey);

        const string partyUInfo = nameof(partyUInfo);
        const string partyVInfo = nameof(partyVInfo);

        using var party1PrivateKey = ECDiffieHellman.Create(Party1Parameters);
        using var party2PrivateKey = ECDiffieHellman.Create(Party2Parameters);

        var party1Pkcs8PrivateKey = party1PrivateKey.ExportPkcs8PrivateKey();
        var party2Pkcs8PrivateKey = party2PrivateKey.ExportPkcs8PrivateKey();

        var keyWrapProvider = new EcdhKeyWrapWithAesKeyWrapProvider(AesKeyWrap.Default, new EccSecretKey(KeyId, KeyBitLength, party1Pkcs8PrivateKey), AlgorithmDescriptor);
        var keyWrapParameters = new EcdhEsKeyWrapWithAesKeyWrapParameters(expectedPlainTextKey, party2PrivateKey, KeyBitLength, partyUInfo, partyVInfo);
        var encryptedKey = keyWrapProvider.WrapKey(keyWrapParameters).ToArray().AsMemory();

        var keyUnwrapProvider = new EcdhKeyWrapWithAesKeyWrapProvider(AesKeyWrap.Default, new EccSecretKey(KeyId, KeyBitLength, party2Pkcs8PrivateKey), AlgorithmDescriptor);
        var keyUnwrapParameters = new EcdhEsKeyUnwrapWithAesKeyUnwrapParameters(encryptedKey, Party1Parameters, KeyBitLength, partyUInfo, partyVInfo);
        var actualPlainTextKey = keyUnwrapProvider.UnwrapKey(keyUnwrapParameters).ToArray();

        var areKeysEqual = expectedPlainTextKey.SequenceEqual(actualPlainTextKey);
        Assert.True(areKeysEqual);
    }
}
