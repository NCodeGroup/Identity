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
using NIdentity.OpenId.Cryptography.Binary;
using NIdentity.OpenId.Cryptography.CryptoProvider.Ecdh;
using NIdentity.OpenId.Cryptography.CryptoProvider.Ecdh.Descriptors;
using NIdentity.OpenId.Cryptography.CryptoProvider.Ecdh.Parameters;
using NIdentity.OpenId.Cryptography.Keys;
using Xunit;

namespace NIdentity.OpenId.Core.Tests.Cryptography.Ecdh;

public class EcdhKeyWrapProviderTests : BaseTests
{
    private const string KeyId = nameof(KeyId);

    // the following test parameters where generated externally with a known working ECDH implementation (jose-jwt)

    private const string ExpectedKeyAgreement = "rpNx7qw/1M36dnIH/OiHXMA/i+D2NHYjqU1XnSex6xE=";
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

    private EcdhKeyWrapAlgorithmDescriptor AlgorithmDescriptor { get; }

    public EcdhKeyWrapProviderTests()
    {
        AlgorithmDescriptor = new EcdhKeyWrapAlgorithmDescriptor(
            AlgorithmCodes.KeyManagement.EcdhEs,
            KeyDerivationFunctionTypes.SP800_56A_CONCAT,
            HashAlgorithmName.SHA256,
            HashBitLength: 256);
    }

    [Fact]
    public void DeriveKey_WhenParty1ToParty2_ThenValid()
    {
        DeriveKey(Party1Parameters, Party2Parameters);
    }

    [Fact]
    public void DeriveKey_WhenParty2ToParty1_ThenValid()
    {
        DeriveKey(Party2Parameters, Party1Parameters);
    }

    private void DeriveKey(ECParameters party1Parameters, ECParameters party2Parameters)
    {
        var mockAgreement = CreateStrictMock<IEcdhEsAgreement>();

        // valid values are 128, 192, or 256
        const int keyBitLength = 256;
        const int keyByteLength = keyBitLength / BinaryUtility.BitsPerByte;

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

        using var party1PrivateKey = ECDiffieHellman.Create(party1Parameters);
        using var party2PrivateKey = ECDiffieHellman.Create(party2Parameters);
        using var party2PublicKey = party2PrivateKey.PublicKey;

        var provider = new EcdhKeyWrapProvider(new EccSecretKey(KeyId, party1Parameters), AlgorithmDescriptor);
        var keyAgreement = provider.DeriveKey(mockAgreement.Object, party1PrivateKey, party2PublicKey);

        Assert.Equal(keyByteLength, keyAgreement.Length);
        Assert.True(keyAgreement.IsSingleSegment);
        Assert.Equal(ExpectedKeyAgreement, Convert.ToBase64String(keyAgreement.FirstSpan));
    }

    [Theory]
    [InlineData(128)]
    [InlineData(192)]
    [InlineData(256)]
    public void RoundTrip_Valid(int keyBitLength)
    {
        var keyByteLength = keyBitLength / BinaryUtility.BitsPerByte;

        const string partyUInfo = nameof(partyUInfo);
        const string partyVInfo = nameof(partyVInfo);

        using var party1PrivateKey = ECDiffieHellman.Create(Party1Parameters);
        using var party2PrivateKey = ECDiffieHellman.Create(Party2Parameters);

        var keyWrapProvider = new EcdhKeyWrapProvider(new EccSecretKey(KeyId, Party1Parameters), AlgorithmDescriptor);
        var keyWrapParameters = new EcdhEsKeyWrapParameters(party2PrivateKey, keyBitLength, partyUInfo, partyVInfo);
        var expectedKeyAgreement = keyWrapProvider.WrapKey(keyWrapParameters).ToArray().AsSpan();
        Assert.Equal(keyByteLength, expectedKeyAgreement.Length);

        using var party1PublicKey = party1PrivateKey.PublicKey;
        var keyUnwrapProvider = new EcdhKeyWrapProvider(new EccSecretKey(KeyId, Party2Parameters), AlgorithmDescriptor);
        var keyUnwrapParameters = new EcdhEsKeyUnwrapParameters(party1PublicKey, keyBitLength, partyUInfo, partyVInfo);
        var actualKeyAgreement = keyUnwrapProvider.UnwrapKey(keyUnwrapParameters).ToArray().AsSpan();
        Assert.Equal(keyByteLength, actualKeyAgreement.Length);

        var areKeysEqual = expectedKeyAgreement.SequenceEqual(actualKeyAgreement);
        Assert.True(areKeysEqual);
    }
}
