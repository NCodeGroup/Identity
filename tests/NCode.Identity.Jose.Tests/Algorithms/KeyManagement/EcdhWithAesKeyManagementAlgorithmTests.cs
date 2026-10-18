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
using System.Text.Json;
using Jose;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Algorithms.KeyManagement;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Keys;
using Base64Url = NCode.Encoders.Base64Url;

namespace NCode.Identity.Jose.Tests.Algorithms.KeyManagement;

public class EcdhWithAesKeyManagementAlgorithmTests : BaseTests
{
    private Mock<IAesKeyWrap> MockAesKeyWrap { get; }

    public EcdhWithAesKeyManagementAlgorithmTests()
    {
        MockAesKeyWrap = CreateStrictMock<IAesKeyWrap>();
    }

    private EcdhWithAesKeyManagementAlgorithm CreateAlgorithm(
        int? kekSizeBits = null,
        IAesKeyWrap? aesKeyWrap = null
    ) => new(aesKeyWrap ?? MockAesKeyWrap.Object, "code", kekSizeBits ?? Random.Shared.Next());

    [Fact]
    public void GetEncryptedContentKeySizeBytes_ReturnsExpected()
    {
        var kekSizeBits = Random.Shared.Next();
        var cekSizeBytes = Random.Shared.Next();
        var encryptedCekSizeBytes = Random.Shared.Next();

        MockAesKeyWrap
            .Setup(x => x.GetEncryptedContentKeySizeBytes(cekSizeBytes))
            .Returns(encryptedCekSizeBytes)
            .Verifiable();

        var algorithm = CreateAlgorithm();
        var result = algorithm.GetEncryptedContentKeySizeBytes(kekSizeBits, cekSizeBytes);
        Assert.Equal(encryptedCekSizeBytes, result);
    }

    public static IEnumerable<object[]> RoundTripTestData
    {
        get
        {
            var curves = new[]
            {
                ECCurve.NamedCurves.nistP256,
                ECCurve.NamedCurves.nistP384,
                ECCurve.NamedCurves.nistP521,
            };

            var keySizes = new[] { 16, 24, 32 };

            foreach (var curve in curves)
            foreach (var keySize in keySizes)
                yield return new object[] { curve, keySize };
        }
    }

    [Theory]
    [MemberData(nameof(RoundTripTestData))]
    public void RoundTrip_EncryptsAndDecrypts(ECCurve curve, int cekSizeBytes)
    {
        const string keyId = nameof(keyId);
        const string alg = nameof(alg);

        var metadata = new KeyMetadata { KeyId = keyId };
        using var key = ECDiffieHellman.Create(curve);
        var secretKey = SecretKeyFactory.CreateEcc(metadata, key);

        var cekSizeBits = cekSizeBytes << 3;
        var algorithm = CreateAlgorithm(cekSizeBits, DefaultAesKeyWrap.Singleton);

        var encryptedCekSizeBytes = algorithm.GetEncryptedContentKeySizeBytes(
            cekSizeBits,
            cekSizeBytes
        );

        Span<byte> apu = new byte[32];
        Span<byte> apv = new byte[32];

        RandomNumberGenerator.Fill(apu);
        RandomNumberGenerator.Fill(apv);

        Span<byte> cek = new byte[cekSizeBytes];
        Span<byte> decryptedCek = new byte[cekSizeBytes];
        var encryptedCekWriter = new ArrayBufferWriter<byte>(encryptedCekSizeBytes);

        var header = new Dictionary<string, object>
        {
            ["alg"] = alg,
            ["apu"] = Base64Url.Encode(apu),
            ["apv"] = Base64Url.Encode(apv),
        };

        algorithm.WrapKey(secretKey, header, cek, encryptedCekWriter);
        Assert.Equal(encryptedCekSizeBytes, encryptedCekWriter.WrittenCount);

        var controlAlgorithm = CreateControlAlgorithm(cekSizeBits);
        var controlResult = controlAlgorithm.Unwrap(
            encryptedCekWriter.WrittenSpan.ToArray(),
            key,
            cekSizeBits,
            header
        );
        Assert.Equal(controlResult, cek.ToArray());

        var headerForUnwrap = JsonSerializer.SerializeToElement(header);
        var unwrapResult = algorithm.TryUnwrapKey(
            secretKey,
            headerForUnwrap,
            encryptedCekWriter.WrittenSpan,
            decryptedCek,
            out var unwrapBytesWritten
        );
        Assert.True(unwrapResult);
        Assert.Equal(cekSizeBytes, unwrapBytesWritten);
        Assert.Equal(controlResult, decryptedCek.ToArray());
    }

    // The BCL-backed (ECDiffieHellman) control implementation; despite the name it is the cross-platform one jose-jwt
    // uses on every platform for managed keys, and the CNG-backed Win variant delegates straight to it for such keys.
    private static EcdhKeyManagementUnixWithAesKeyWrap CreateControlAlgorithm(int cekSizeBits)
    {
        var aesKw = new AesKeyWrapManagement(cekSizeBits);
        return new EcdhKeyManagementUnixWithAesKeyWrap(cekSizeBits, aesKw);
    }
}
