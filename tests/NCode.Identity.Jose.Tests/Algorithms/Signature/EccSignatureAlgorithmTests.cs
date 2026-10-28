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
using Jose.netstandard1_4;
using NCode.Identity.Jose.Algorithms.Signature;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.Tests.Algorithms.Signature;

public class EccSignatureAlgorithmTests : BaseTests
{
    [Fact]
    public void Code_ReturnsCode()
    {
        const string code = nameof(code);
        var anyValidHashAlgorithmName = HashAlgorithmName.SHA384;

        var algorithm = new EccSignatureAlgorithm(
            code,
            ECCurve.NamedCurves.nistP384,
            anyValidHashAlgorithmName
        );
        Assert.Equal(code, algorithm.Code);
    }

    [Fact]
    public void KeyType_ReturnsEccSecretKey()
    {
        const string code = nameof(code);
        var anyValidHashAlgorithmName = HashAlgorithmName.SHA384;

        var algorithm = new EccSignatureAlgorithm(
            code,
            ECCurve.NamedCurves.nistP384,
            anyValidHashAlgorithmName
        );
        Assert.Equal(typeof(EccSecretKey), algorithm.KeyType);
    }

    public static IEnumerable<object[]> GetKeyBitSizesTestData()
    {
        yield return new object[] { ECCurve.NamedCurves.nistP256, HashAlgorithmName.SHA256, 256 };
        yield return new object[] { ECCurve.NamedCurves.nistP384, HashAlgorithmName.SHA384, 384 };
        yield return new object[] { ECCurve.NamedCurves.nistP521, HashAlgorithmName.SHA512, 521 };
    }

    [Theory]
    [MemberData(nameof(GetKeyBitSizesTestData))]
    public void KeyBitSizes_WhenGivenHashAlgorithm_ReturnsExpected(
        ECCurve curve,
        HashAlgorithmName hashAlgorithmName,
        int expected
    )
    {
        const string code = nameof(code);

        var algorithm = new EccSignatureAlgorithm(code, curve, hashAlgorithmName);
        var result = Assert.Single(algorithm.KeyBitSizes);
        Assert.Equal(expected, result.MinSize);
        Assert.Equal(expected, result.MaxSize);
        Assert.Equal(0, result.SkipSize);
    }

    public static IEnumerable<object[]> GetSignatureSizeBitsTestData()
    {
        // ECDSA signatures are twice the size of the key size rounded up to the nearest byte
        yield return new object[] { ECCurve.NamedCurves.nistP256, HashAlgorithmName.SHA256, 64 };
        yield return new object[] { ECCurve.NamedCurves.nistP384, HashAlgorithmName.SHA384, 96 };
        yield return new object[] { ECCurve.NamedCurves.nistP521, HashAlgorithmName.SHA512, 132 };
    }

    [Theory]
    [MemberData(nameof(GetSignatureSizeBitsTestData))]
    public void GetSignatureSizeBytes_WhenGivenHashAlgorithm_ReturnsExpected(
        ECCurve curve,
        HashAlgorithmName hashAlgorithmName,
        int expected
    )
    {
        const string code = nameof(code);

        var keySizeBits = Random.Shared.Next();

        var algorithm = new EccSignatureAlgorithm(code, curve, hashAlgorithmName);
        var result = algorithm.GetSignatureSizeBytes(keySizeBits);
        Assert.Equal(expected, result);
    }

    public static IEnumerable<object[]> GetRoundTripTestData()
    {
        yield return new object[] { ECCurve.NamedCurves.nistP256, HashAlgorithmName.SHA256 };
        yield return new object[] { ECCurve.NamedCurves.nistP384, HashAlgorithmName.SHA384 };
        yield return new object[] { ECCurve.NamedCurves.nistP521, HashAlgorithmName.SHA512 };
    }

    [Theory]
    [MemberData(nameof(GetRoundTripTestData))]
    public void RoundTrip_SignsAndVerifies(ECCurve curve, HashAlgorithmName hashAlgorithmName)
    {
        const string keyId = nameof(keyId);
        const string code = nameof(code);

        var metadata = new KeyMetadata { KeyId = keyId };
        using var key = ECDsa.Create(curve);
        var secretKey = SecretKeyFactory.CreateEcc(metadata, key);

        var algorithm = new EccSignatureAlgorithm(code, curve, hashAlgorithmName);
        var hashSizeBytes = algorithm.GetSignatureSizeBytes(secretKey.KeySizeBits);

        var dataSizeBytes = Random.Shared.Next(128, 1024);
        Span<byte> inputData = stackalloc byte[dataSizeBytes];
        Span<byte> signature = stackalloc byte[hashSizeBytes];

        RandomNumberGenerator.Fill(inputData);

        var signResult = algorithm.TrySign(
            secretKey,
            inputData,
            signature,
            out var signBytesWritten
        );
        Assert.True(signResult);
        Assert.Equal(hashSizeBytes, signBytesWritten);

        var controlAlgorithm = new EcdsaUsingSha(secretKey.KeySizeBits);
        var controlHash = controlAlgorithm.Sign(inputData.ToArray(), key);
        Assert.Equal(hashSizeBytes, controlHash.Length);

        var verifyResult = algorithm.Verify(secretKey, inputData, signature);
        Assert.True(verifyResult);

        var verifyHashFromControl = algorithm.Verify(secretKey, inputData, controlHash);
        Assert.True(verifyHashFromControl);

        var verifyHashUsingControl = controlAlgorithm.Verify(
            signature.ToArray(),
            inputData.ToArray(),
            key
        );
        Assert.True(verifyHashUsingControl);
    }

    private static EccSignatureAlgorithm CreateEs256Algorithm() =>
        new("ES256", ECCurve.NamedCurves.nistP256, HashAlgorithmName.SHA256);

    private static EccSecretKey CreateBrainpoolP256Key()
    {
        var metadata = new KeyMetadata { KeyId = "keyId" };
        using var key = ECDsa.Create(ECCurve.NamedCurves.brainpoolP256r1);
        return SecretKeyFactory.CreateEcc(metadata, key);
    }

    [Fact]
    public void IsKeyCompatible_WhenCurveMatches_ReturnsTrue()
    {
        var metadata = new KeyMetadata { KeyId = "keyId" };
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var secretKey = SecretKeyFactory.CreateEcc(metadata, key);

        var algorithm = CreateEs256Algorithm();
        Assert.True(algorithm.IsKeyCompatible(secretKey));
    }

    [Fact]
    public void IsKeyCompatible_WhenSameSizeDifferentCurve_ReturnsFalse()
    {
        var secretKey = CreateBrainpoolP256Key();

        var algorithm = CreateEs256Algorithm();
        Assert.False(algorithm.IsKeyCompatible(secretKey));
    }

    public static IEnumerable<object[]> GetLargerCurveMatchesTestData()
    {
        yield return ["ES384", ECCurve.NamedCurves.nistP384, HashAlgorithmName.SHA384];
        yield return ["ES512", ECCurve.NamedCurves.nistP521, HashAlgorithmName.SHA512];
    }

    [Theory]
    [MemberData(nameof(GetLargerCurveMatchesTestData))]
    public void IsKeyCompatible_WhenEs384OrEs512CurveMatches_ReturnsTrue(
        string code,
        ECCurve curve,
        HashAlgorithmName hashAlgorithmName
    )
    {
        var metadata = new KeyMetadata { KeyId = "keyId" };
        using var key = ECDsa.Create(curve);
        var secretKey = SecretKeyFactory.CreateEcc(metadata, key);

        var algorithm = new EccSignatureAlgorithm(code, curve, hashAlgorithmName);
        Assert.True(algorithm.IsKeyCompatible(secretKey));
    }

    [Fact]
    public void IsKeyCompatible_WhenEs384AndSameSizeDifferentCurve_ReturnsFalse()
    {
        var metadata = new KeyMetadata { KeyId = "keyId" };
        using var key = ECDsa.Create(ECCurve.NamedCurves.brainpoolP384r1);
        var secretKey = SecretKeyFactory.CreateEcc(metadata, key);

        var algorithm = new EccSignatureAlgorithm(
            "ES384",
            ECCurve.NamedCurves.nistP384,
            HashAlgorithmName.SHA384
        );

        Assert.Equal(384, secretKey.KeySizeBits);
        Assert.False(algorithm.IsKeyCompatible(secretKey));
    }

    [Fact]
    public void IsKeyCompatible_WhenEs512AndSameSizeDifferentCurve_ReturnsFalse()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.brainpoolP512r1);
        var secretKey = new StubEccSecretKey(key, curveSizeBits: 521);

        var algorithm = new EccSignatureAlgorithm(
            "ES512",
            ECCurve.NamedCurves.nistP521,
            HashAlgorithmName.SHA512
        );

        Assert.Equal(521, secretKey.KeySizeBits);
        Assert.False(algorithm.IsKeyCompatible(secretKey));
    }

    [Fact]
    public void TrySign_WhenSameSizeDifferentCurve_Throws()
    {
        var secretKey = CreateBrainpoolP256Key();
        var algorithm = CreateEs256Algorithm();
        var signature = new byte[64];

        Assert.Throws<ArgumentException>(() =>
            algorithm.TrySign(secretKey, new byte[16], signature, out _)
        );
    }

    [Fact]
    public void Verify_WhenSameSizeDifferentCurve_Throws()
    {
        var secretKey = CreateBrainpoolP256Key();
        var algorithm = CreateEs256Algorithm();

        Assert.Throws<ArgumentException>(() =>
            algorithm.Verify(secretKey, new byte[16], new byte[64])
        );
    }

    private sealed class StubEccSecretKey(ECDsa ecdsa, int curveSizeBits)
        : EccSecretKey(new KeyMetadata { KeyId = "keyId" }, curveSizeBits, certificateRawData: null)
    {
        public override ECDsa ExportECDsa() =>
            ECDsa.Create(ecdsa.ExportParameters(includePrivateParameters: true));

        public override ECDiffieHellman ExportECDiffieHellman() =>
            throw new NotSupportedException();
    }
}
