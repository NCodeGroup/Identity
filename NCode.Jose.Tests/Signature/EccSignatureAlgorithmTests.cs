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
using NCode.Jose.Signature;

namespace NCode.Jose.Tests.Signature;

public class EccSignatureAlgorithmTests
{
    [Fact]
    public void Code_Valid()
    {
        const string code = nameof(code);

        var algorithm = new EccSignatureAlgorithm(code, HashAlgorithmName.SHA512);
        Assert.Equal(code, algorithm.Code);
    }

    [Fact]
    public void KeyType_Valid()
    {
        const string code = nameof(code);

        var algorithm = new EccSignatureAlgorithm(code, HashAlgorithmName.SHA512);
        Assert.Equal(typeof(EccSecretKey), algorithm.KeyType);
    }

    public static IEnumerable<object[]> GetKeyBitSizesTestData()
    {
        yield return new object[] { HashAlgorithmName.SHA256, 256 };
        yield return new object[] { HashAlgorithmName.SHA384, 384 };
        yield return new object[] { HashAlgorithmName.SHA512, 521 };
    }

    [Theory]
    [MemberData(nameof(GetKeyBitSizesTestData))]
    public void KeyBitSizes_Valid(HashAlgorithmName hashAlgorithmName, int expected)
    {
        const string code = nameof(code);

        var algorithm = new EccSignatureAlgorithm(code, hashAlgorithmName);
        var result = Assert.Single(algorithm.KeyBitSizes);
        Assert.Equal(expected, result.MinSize);
        Assert.Equal(expected, result.MaxSize);
        Assert.Equal(0, result.SkipSize);
    }

    public static IEnumerable<object[]> GetSignatureSizeBitsTestData()
    {
        // ECDSA signatures are twice the size of the key size rounded up to the nearest byte
        yield return new object[] { HashAlgorithmName.SHA256, 64 };
        yield return new object[] { HashAlgorithmName.SHA384, 96 };
        yield return new object[] { HashAlgorithmName.SHA512, 132 };
    }

    [Theory]
    [MemberData(nameof(GetSignatureSizeBitsTestData))]
    public void GetSignatureSizeBytes_Valid(HashAlgorithmName hashAlgorithmName, int expected)
    {
        const string code = nameof(code);

        var keySizeBits = Random.Shared.Next();

        var algorithm = new EccSignatureAlgorithm(code, hashAlgorithmName);
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
    public void RoundTrip_Valid(ECCurve curve, HashAlgorithmName hashAlgorithmName)
    {
        const string keyId = nameof(keyId);
        const string code = nameof(code);

        using var key = ECDsa.Create(curve);
        using var secretKey = EccSecretKey.Create(keyId, key);

        var algorithm = new EccSignatureAlgorithm(code, hashAlgorithmName);
        var hashSizeBytes = algorithm.GetSignatureSizeBytes(secretKey.KeySizeBits);

        var dataSizeBytes = Random.Shared.Next(128, 1024);
        Span<byte> inputData = stackalloc byte[dataSizeBytes];
        Span<byte> signature = stackalloc byte[hashSizeBytes];

        RandomNumberGenerator.Fill(inputData);

        var signResult = algorithm.TrySign(secretKey, inputData, signature, out var signBytesWritten);
        Assert.True(signResult);
        Assert.Equal(hashSizeBytes, signBytesWritten);

        var controlAlgorithm = new global::Jose.netstandard1_4.EcdsaUsingSha(secretKey.KeySizeBits);
        var controlHash = controlAlgorithm.Sign(inputData.ToArray(), key);
        Assert.Equal(hashSizeBytes, controlHash.Length);

        var verifyResult = algorithm.Verify(secretKey, inputData, signature);
        Assert.True(verifyResult);

        var verifyHashFromControl = algorithm.Verify(secretKey, inputData, controlHash);
        Assert.True(verifyHashFromControl);

        var verifyHashUsingControl = controlAlgorithm.Verify(signature.ToArray(), inputData.ToArray(), key);
        Assert.True(verifyHashUsingControl);
    }
}
