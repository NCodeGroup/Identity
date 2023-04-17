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
using System.Text.Json;
using Jose;
using Moq;
using NIdentity.OpenId.Cryptography.CryptoProvider.Ecdsa.Descriptors;
using NIdentity.OpenId.Cryptography.CryptoProvider.Noop;
using NIdentity.OpenId.Cryptography.CryptoProvider.Rsa.Descriptors;
using NIdentity.OpenId.Cryptography.CryptoProvider.Signature;
using NIdentity.OpenId.Cryptography.CryptoProvider.Signature.Descriptors;
using NIdentity.OpenId.Cryptography.Descriptors;
using NIdentity.OpenId.Cryptography.Keys;
using NIdentity.OpenId.Jose;
using Xunit;

namespace NIdentity.OpenId.Core.Tests.Jose;

public class JoseSerializerTests : BaseTests
{
    private Mock<ISecretKeyCollection> MockSecretKeyCollection { get; }
    private Mock<IAlgorithmCollection> MockAlgorithmCollection { get; }
    private Mock<JoseSerializer> MockJoseSerializer { get; }
    private JoseSerializer JoseSerializer { get; }

    public JoseSerializerTests()
    {
        MockSecretKeyCollection = CreateStrictMock<ISecretKeyCollection>();
        MockAlgorithmCollection = CreateStrictMock<IAlgorithmCollection>();
        MockJoseSerializer = CreatePartialMock<JoseSerializer>(MockAlgorithmCollection.Object);
        JoseSerializer = MockJoseSerializer.Object;
    }

    private void Decode_Valid(SignatureAlgorithmDescriptor algorithm, Func<SecretKey, object>? exportKey)
    {
        Decode_Valid(true, algorithm, exportKey);
        Decode_Valid(false, algorithm, exportKey);
    }

    private void Decode_Valid(bool useSpecificKey, SignatureAlgorithmDescriptor algorithm, Func<SecretKey, object>? exportKey)
    {
        const string keyId = nameof(keyId);

        var algorithmCode = algorithm.AlgorithmCode;
        var jwsAlgorithm = JWT.DefaultSettings.JwsAlgorithmFromHeader(algorithmCode);

        using var secretKey = exportKey is not null ?
            algorithm.CryptoFactory.GenerateNewKey(keyId, algorithm, keySizeBitsHint: 512) :
            null;

        var key = exportKey is not null && secretKey is not null ?
            exportKey(secretKey) :
            null;

        using var disposableKey = key as IDisposable ?? CreateLooseMock<IDisposable>().Object;

        var data = new Dictionary<string, object>
        {
            ["key1"] = "value1"
        };

        var extraHeaders = new Dictionary<string, object>();
        if (useSpecificKey)
        {
            extraHeaders["kid"] = keyId;
        }

        var json = JsonSerializer.Serialize(data);
        var jwt = JWT.Encode(json, key, jwsAlgorithm, extraHeaders);

        if (secretKey != null)
        {
            var outAlgorithm = algorithm;
            MockAlgorithmCollection
                .Setup(_ => _.TryGetSignatureAlgorithm(algorithmCode, out outAlgorithm))
                .Returns(true)
                .Verifiable();

            if (useSpecificKey)
            {
                var outSecretKey = secretKey;
                MockSecretKeyCollection
                    .Setup(_ => _.TryGet(keyId, out outSecretKey))
                    .Returns(true)
                    .Verifiable();
            }
            else
            {
                var keys = new List<SecretKey> { secretKey };
                IEnumerator<SecretKey> enumerator = keys.GetEnumerator();

                MockSecretKeyCollection
                    .Setup(_ => _.GetEnumerator())
                    .Returns(enumerator)
                    .Verifiable();
            }
        }

        var result = JoseSerializer.Decode(jwt, MockSecretKeyCollection.Object);
        Assert.Equal(json, result);
    }

    [Fact]
    public void Decode_NoSig_Valid()
    {
        var algorithm = new SignatureAlgorithmDescriptor(
            NoopCryptoFactory.Default,
            typeof(SecretKey),
            AlgorithmCodes.DigitalSignature.None,
            HashSizeBits: 0);

        Decode_Valid(algorithm, null);
    }

    [Fact]
    public void Decode_HS256_Valid()
    {
        var algorithm = new KeyedHashAlgorithmDescriptor(
            HMACSHA256.TryHashData,
            KeyedHashAlgorithmCryptoFactory.Default,
            typeof(SymmetricSecretKey),
            AlgorithmCodes.DigitalSignature.HmacSha256,
            HashSizeBits: 256);

        Decode_Valid(algorithm, secretKey => ((SymmetricSecretKey)secretKey).KeyBytes.ToArray());
    }

    [Fact]
    public void Decode_RS384_Valid()
    {
        var algorithm = new RsaSignatureAlgorithmDescriptor(
            AlgorithmCodes.DigitalSignature.RsaSha384,
            HashAlgorithmName.SHA384,
            HashSizeBytes: 384,
            RSASignaturePadding.Pkcs1);

        Decode_Valid(algorithm, secretKey => ((RsaSecretKey)secretKey).CreateRSA());
    }

    [Fact]
    public void Decode_PS256_Valid()
    {
        var algorithm = new RsaSignatureAlgorithmDescriptor(
            AlgorithmCodes.DigitalSignature.RsaSsaPssSha256,
            HashAlgorithmName.SHA256,
            HashSizeBytes: 256,
            RSASignaturePadding.Pss);

        Decode_Valid(algorithm, secretKey => ((RsaSecretKey)secretKey).CreateRSA());
    }

    [Fact]
    public void Decode_ES512_Valid()
    {
        var algorithm = new EcdsaHashSignatureAlgorithmDescriptor(
            AlgorithmCodes.DigitalSignature.EcdsaSha512,
            HashAlgorithmName.SHA512,
            HashSizeBits: 512,
            KeySizeBits: 521);

        Decode_Valid(algorithm, secretKey => ((EccSecretKey)secretKey).CreateECDsa());
    }
}
