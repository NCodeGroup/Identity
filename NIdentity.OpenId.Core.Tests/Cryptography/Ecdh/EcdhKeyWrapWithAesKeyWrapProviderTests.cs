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
using Moq;
using NIdentity.OpenId.Cryptography.Aes;
using NIdentity.OpenId.Cryptography.CryptoProvider;
using NIdentity.OpenId.Cryptography.Ecdh;
using NIdentity.OpenId.DataContracts;
using Xunit;

namespace NIdentity.OpenId.Core.Tests.Cryptography.Ecdh;

public class EcdhKeyWrapWithAesKeyWrapProviderTests : BaseTests
{
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

    private ECDiffieHellman Party1 { get; }
    private ECDiffieHellman Party2 { get; }
    private EcdhKeyWrapWithAesKeyWrapAlgorithmDescriptor AlgorithmDescriptor { get; }

    private Mock<ICryptoFactory> MockCryptoFactory { get; }

    public EcdhKeyWrapWithAesKeyWrapProviderTests()
    {
        MockCryptoFactory = CreateStrictMock<ICryptoFactory>();

        Party1 = ECDiffieHellman.Create(Party1Parameters);
        Party2 = ECDiffieHellman.Create(Party2Parameters);

        AlgorithmDescriptor = new EcdhKeyWrapWithAesKeyWrapAlgorithmDescriptor(
            MockCryptoFactory.Object,
            AlgorithmCode,
            KeyDerivationFunctionTypes.SP800_56A_CONCAT,
            HashAlgorithmName.SHA256,
            HashBitLength: 256,
            KeyBitLength);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Party2.Dispose();
            Party1.Dispose();
        }

        base.Dispose(disposing);
    }

    [Fact]
    public void WrapKey_WhenParty1ToParty2_ThenValid()
    {
        WrapKey(Party1, Party2);
    }

    [Fact]
    public void WrapKey_WhenParty2ToParty1_ThenValid()
    {
        WrapKey(Party2, Party1);
    }

    private void WrapKey(ECDiffieHellman party1, ECDiffieHellman party2)
    {
        const string partyUInfo = nameof(partyUInfo);
        const string partyVInfo = nameof(partyVInfo);

        var provider = new EcdhKeyWrapWithAesKeyWrapProvider(
            AesKeyWrap.Default,
            new EcdhSecretKey(new Secret(), party1),
            AlgorithmDescriptor);

        var plainTextKey = Convert.FromBase64String(PlainTextKey);
        var parameters = new EcdhEsKeyWrapWithAesKeyWrapParameters(
            plainTextKey,
            party2,
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

        var keyWrapProvider = new EcdhKeyWrapWithAesKeyWrapProvider(AesKeyWrap.Default, new EcdhSecretKey(new Secret(), Party1), AlgorithmDescriptor);
        var keyWrapParameters = new EcdhEsKeyWrapWithAesKeyWrapParameters(expectedPlainTextKey, Party2, KeyBitLength, partyUInfo, partyVInfo);
        var encryptedKey = keyWrapProvider.WrapKey(keyWrapParameters).ToArray().AsMemory();

        using var party1PublicKey = Party1.PublicKey;
        var keyUnwrapProvider = new EcdhKeyWrapWithAesKeyWrapProvider(AesKeyWrap.Default, new EcdhSecretKey(new Secret(), Party2), AlgorithmDescriptor);
        var keyUnwrapParameters = new EcdhEsKeyUnwrapWithAesKeyUnwrapParameters(encryptedKey, party1PublicKey, KeyBitLength, partyUInfo, partyVInfo);
        var actualPlainTextKey = keyUnwrapProvider.UnwrapKey(keyUnwrapParameters).ToArray();

        var areKeysEqual = expectedPlainTextKey.SequenceEqual(actualPlainTextKey);
        Assert.True(areKeysEqual);
    }
}
