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
using Moq;
using NIdentity.OpenId.Cryptography.CryptoProvider;
using NIdentity.OpenId.Cryptography.Ecdh;
using NIdentity.OpenId.DataContracts;
using Xunit;

namespace NIdentity.OpenId.Core.Tests.Cryptography.Ecdh;

public class EcdhKeyWrapProviderTests : BaseTests
{
    // the following test parameters where generated externally with a known working ECDH implementation (jose-jwt)

    private const string ExpectedDerivedKey = "rpNx7qw/1M36dnIH/OiHXMA/i+D2NHYjqU1XnSex6xE=";
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
    private EcdhKeyWrapAlgorithmDescriptor AlgorithmDescriptor { get; }

    private Mock<ICryptoFactory> MockCryptoFactory { get; }

    public EcdhKeyWrapProviderTests()
    {
        MockCryptoFactory = CreateStrictMock<ICryptoFactory>();

        Party1 = ECDiffieHellman.Create(Party1Parameters);
        Party2 = ECDiffieHellman.Create(Party2Parameters);

        AlgorithmDescriptor = new EcdhKeyWrapAlgorithmDescriptor(
            MockCryptoFactory.Object,
            AlgorithmCodes.KeyManagement.EcdhEs,
            KeyDerivationFunctionTypes.SP800_56A_CONCAT,
            HashAlgorithmName.SHA256,
            256);
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
    public void DeriveKey_Party1_To_Party2()
    {
        DeriveKey(Party1, Party2);
    }

    [Fact]
    public void DeriveKey_Party2_To_Party1()
    {
        DeriveKey(Party2, Party1);
    }

    private void DeriveKey(ECDiffieHellman party1, ECDiffieHellman party2)
    {
        var mockAgreement = CreateStrictMock<IEcdhEsAgreement>();

        const int keyBitLength = 256;
        const int keyByteLength = keyBitLength / 8;

        const string partyUInfo = nameof(partyUInfo);
        const string partyVInfo = nameof(partyVInfo);

        mockAgreement
            .Setup(_ => _.KeyBitLength)
            .Returns(keyBitLength)
            .Verifiable();

        mockAgreement
            .Setup(_ => _.PartyUInfo)
            .Returns(partyUInfo)
            .Verifiable();

        mockAgreement
            .Setup(_ => _.PartyVInfo)
            .Returns(partyVInfo)
            .Verifiable();

        using var party2PublicKey = party2.PublicKey;
        var provider = new EcdhKeyWrapProvider(new EcdhSecretKey(new Secret(), party1), AlgorithmDescriptor);
        var derivedKey = provider.DeriveKey(mockAgreement.Object, party1, party2PublicKey);

        Assert.Equal(keyByteLength, derivedKey.Length);
        Assert.True(derivedKey.IsSingleSegment);
        Assert.Equal(ExpectedDerivedKey, Convert.ToBase64String(derivedKey.FirstSpan));
    }

    [Fact]
    public void RoundTrip_Valid()
    {
        const int keyBitLength = 256;
        const int keyByteLength = keyBitLength / 8;

        const string partyUInfo = nameof(partyUInfo);
        const string partyVInfo = nameof(partyVInfo);

        var provider1 = new EcdhKeyWrapProvider(new EcdhSecretKey(new Secret(), Party1), AlgorithmDescriptor);
        var keyWrapParameters = new EcdhEsKeyWrapParameters(Party2, keyBitLength, partyUInfo, partyVInfo);
        var derivedKey1 = provider1.WrapKey(keyWrapParameters);

        Assert.Equal(keyByteLength, derivedKey1.Length);
        Assert.True(derivedKey1.IsSingleSegment);
        Assert.Equal(ExpectedDerivedKey, Convert.ToBase64String(derivedKey1.FirstSpan));

        using var party1PublicKey = Party1.PublicKey;
        var provider2 = new EcdhKeyWrapProvider(new EcdhSecretKey(new Secret(), Party2), AlgorithmDescriptor);
        var keyUnwrapParameters = new EcdhEsKeyUnwrapParameters(party1PublicKey, keyBitLength, partyUInfo, partyVInfo);
        var derivedKey2 = provider2.UnwrapKey(keyUnwrapParameters);

        Assert.Equal(keyByteLength, derivedKey2.Length);
        Assert.True(derivedKey2.IsSingleSegment);
        Assert.Equal(ExpectedDerivedKey, Convert.ToBase64String(derivedKey2.FirstSpan));
    }
}
