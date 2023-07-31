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

public class KeyedHashSignatureAlgorithmTests
{
    [Fact]
    public void Code_Valid()
    {
        const string code = nameof(code);

        var signatureSizeBits = Random.Shared.Next();

        var algorithm = new KeyedHashSignatureAlgorithm(code, signatureSizeBits, null!);
        Assert.Equal(code, algorithm.Code);
    }

    [Fact]
    public void KeyType_Valid()
    {
        const string code = nameof(code);

        var signatureSizeBits = Random.Shared.Next();

        var algorithm = new KeyedHashSignatureAlgorithm(code, signatureSizeBits, null!);
        Assert.Equal(typeof(SymmetricSecretKey), algorithm.KeyType);
    }

    [Fact]
    public void KeyBitSizes_Valid()
    {
        const string code = nameof(code);

        var signatureSizeBits = Random.Shared.Next();

        var algorithm = new KeyedHashSignatureAlgorithm(code, signatureSizeBits, null!);
        var result = Assert.Single(algorithm.KeyBitSizes);
        Assert.Equal(signatureSizeBits, result.MinSize);
        Assert.Equal(int.MaxValue, result.MaxSize);
        Assert.Equal(8, result.SkipSize);
    }

    [Fact]
    public void GetSignatureSizeBytes_Valid()
    {
        const string code = nameof(code);

        var keySizeBits = Random.Shared.Next(128, 1024);
        var signatureSizeBytes = Random.Shared.Next(128, 1024);
        var signatureSizeBits = signatureSizeBytes * 8;

        var algorithm = new KeyedHashSignatureAlgorithm(code, signatureSizeBits, null!);
        var result = algorithm.GetSignatureSizeBytes(keySizeBits);
        Assert.Equal(signatureSizeBytes, result);
    }

    public static IEnumerable<object[]> GetRoundTripTestData()
    {
        yield return new object[] { 256, 256, (KeyedHashFunctionDelegate)HMACSHA256.TryHashData };
        yield return new object[] { 384, 256, (KeyedHashFunctionDelegate)HMACSHA256.TryHashData };
        yield return new object[] { 512, 256, (KeyedHashFunctionDelegate)HMACSHA256.TryHashData };
        yield return new object[] { 768, 256, (KeyedHashFunctionDelegate)HMACSHA256.TryHashData };

        yield return new object[] { 384, 384, (KeyedHashFunctionDelegate)HMACSHA384.TryHashData };
        yield return new object[] { 512, 384, (KeyedHashFunctionDelegate)HMACSHA384.TryHashData };
        yield return new object[] { 768, 384, (KeyedHashFunctionDelegate)HMACSHA384.TryHashData };

        yield return new object[] { 512, 512, (KeyedHashFunctionDelegate)HMACSHA512.TryHashData };
        yield return new object[] { 768, 512, (KeyedHashFunctionDelegate)HMACSHA512.TryHashData };
    }

    [Theory]
    [MemberData(nameof(GetRoundTripTestData))]
    public void RoundTrip_Valid(int keySizeBits, int signatureSizeBits, KeyedHashFunctionDelegate keyedHashFunction)
    {
        const string keyId = nameof(keyId);
        const string code = nameof(code);

        var keySizeBytes = keySizeBits >> 3;
        var hashSizeBytes = signatureSizeBits >> 3;
        var dataSizeBytes = Random.Shared.Next(128, 1024);
        Span<byte> key = new byte[keySizeBytes];
        Span<byte> inputData = stackalloc byte[dataSizeBytes];
        Span<byte> signature = stackalloc byte[hashSizeBytes];

        RandomNumberGenerator.Fill(key);
        RandomNumberGenerator.Fill(inputData);

        using var secretKey = new SymmetricSecretKey(keyId, key);

        var algorithm = new KeyedHashSignatureAlgorithm(code, signatureSizeBits, keyedHashFunction);

        var signResult = algorithm.TrySign(secretKey, inputData, signature, out var signBytesWritten);
        Assert.True(signResult);
        Assert.Equal(hashSizeBytes, signBytesWritten);

        var controlAlgorithm = new global::Jose.HmacUsingSha($"SHA{signatureSizeBits}");
        var controlHash = controlAlgorithm.Sign(inputData.ToArray(), key.ToArray());
        Assert.Equal(hashSizeBytes, controlHash.Length);

        var verifyResult = algorithm.Verify(secretKey, inputData, signature);
        Assert.True(verifyResult);

        var verifyHashFromControl = algorithm.Verify(secretKey, inputData, controlHash);
        Assert.True(verifyHashFromControl);

        var verifyHashUsingControl = controlAlgorithm.Verify(signature.ToArray(), inputData.ToArray(), key.ToArray());
        Assert.True(verifyHashUsingControl);
    }
}
