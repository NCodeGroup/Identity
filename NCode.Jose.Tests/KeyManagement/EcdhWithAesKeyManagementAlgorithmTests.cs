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
using NCode.Cryptography.Keys;
using NCode.Encoders;
using NCode.Jose.KeyManagement;

namespace NCode.Jose.Tests.KeyManagement;

public class EcdhWithAesKeyManagementAlgorithmTests : BaseTests
{
    private Mock<IAesKeyWrap> MockAesKeyWrap { get; }

    public EcdhWithAesKeyManagementAlgorithmTests()
    {
        MockAesKeyWrap = CreateStrictMock<IAesKeyWrap>();
    }

    private EcdhWithAesKeyManagementAlgorithm CreateAlgorithm(
        int? kekSizeBits = null,
        IAesKeyWrap? aesKeyWrap = null) =>
        new(
            aesKeyWrap ?? MockAesKeyWrap.Object,
            "code",
            kekSizeBits ?? Random.Shared.Next());

    [Fact]
    public void GetEncryptedContentKeySizeBytes_Valid()
    {
        var kekSizeBits = Random.Shared.Next();
        var cekSizeBytes = Random.Shared.Next();
        var encryptedCekSizeBytes = Random.Shared.Next();

        MockAesKeyWrap
            .Setup(_ => _.GetEncryptedContentKeySizeBytes(cekSizeBytes))
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
                ECCurve.NamedCurves.nistP521
            };

            var keySizes = new[] { 16, 24, 32 };

            foreach (var curve in curves)
            foreach (var keySize in keySizes)
                yield return new object[] { curve, keySize };
        }
    }

    [Theory]
    [MemberData(nameof(RoundTripTestData))]
    public void RoundTrip_Valid(ECCurve curve, int cekSizeBytes)
    {
        const string keyId = nameof(keyId);
        const string alg = nameof(alg);

        using var key = ECDiffieHellman.Create(curve);
        using var secretKey = EccSecretKey.Create(keyId, Array.Empty<string>(), key);

        var cekSizeBits = cekSizeBytes << 3;
        var algorithm = CreateAlgorithm(cekSizeBits, AesKeyWrap.Default);

        var encryptedCekSizeBytes = algorithm.GetEncryptedContentKeySizeBytes(cekSizeBits, cekSizeBytes);
        var parameters = key.ExportParameters(includePrivateParameters: true);

        Span<byte> apu = new byte[32];
        Span<byte> apv = new byte[32];

        RandomNumberGenerator.Fill(apu);
        RandomNumberGenerator.Fill(apv);

        Span<byte> cek = new byte[cekSizeBytes];
        Span<byte> encryptedCek = new byte[encryptedCekSizeBytes];

        var header = new Dictionary<string, object>
        {
            ["alg"] = alg,
            ["apu"] = Base64Url.Encode(apu),
            ["apv"] = Base64Url.Encode(apv),
        };

        var wrapResult = algorithm.TryWrapKey(secretKey, header, cek, encryptedCek, out var wrapBytesWritten);
        Assert.True(wrapResult);
        Assert.Equal(encryptedCekSizeBytes, wrapBytesWritten);

        using var controlKey = global::Jose.keys.EccKey.New(
            parameters.Q.X,
            parameters.Q.Y,
            parameters.D,
            CngKeyUsages.KeyAgreement);
        var controlAlgorithm = new global::Jose.EcdhKeyManagementWithAesKeyWrap(
            cekSizeBits,
            new global::Jose.AesKeyWrapManagement(cekSizeBits));
        var controlResult = controlAlgorithm.Unwrap(encryptedCek.ToArray(), controlKey, cekSizeBits, header);
        Assert.Equal(controlResult, cek.ToArray());

        var unwrapResult = algorithm.TryUnwrapKey(secretKey, header, encryptedCek, cek, out var unwrapBytesWritten);
        Assert.True(unwrapResult);
        Assert.Equal(cekSizeBytes, unwrapBytesWritten);
        Assert.Equal(controlResult, cek.ToArray());
    }
}
