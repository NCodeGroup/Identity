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
using NCode.Jose.Algorithms;
using NCode.Jose.Algorithms.Signature;
using NCode.Jose.Extensions;
using NCode.Jose.SecretKeys;

namespace NCode.Jose.Tests.Algorithms.Signature;

public class RsaSignatureAlgorithmTests
{
    private static SecretKeyFactory SecretKeyFactory { get; } = new();

    [Fact]
    public void Code_Valid()
    {
        const string code = nameof(code);

        var algorithm = new RsaSignatureAlgorithm(code, default, null!);
        Assert.Equal(code, algorithm.Code);
    }

    [Fact]
    public void KeyType_Valid()
    {
        const string code = nameof(code);

        var algorithm = new RsaSignatureAlgorithm(code, default, null!);
        Assert.Equal(typeof(RsaSecretKey), algorithm.KeyType);
    }

    [Fact]
    public void KeyBitSizes_Valid()
    {
        const string code = nameof(code);

        var algorithm = new RsaSignatureAlgorithm(code, default, null!);
        var result = Assert.Single(algorithm.KeyBitSizes);
        Assert.Equal(2048, result.MinSize);
        Assert.Equal(16384, result.MaxSize);
        Assert.Equal(64, result.SkipSize);
    }

    public static IEnumerable<object[]> GetSignatureSizeBitsTestData()
    {
        var keyBitSizes = new[] { 2048, 2048 + 8, 4096, 4096 + 16 };

        foreach (var keyBitSize in keyBitSizes)
        {
            yield return new object[] { keyBitSize };
        }
    }

    [Theory]
    [MemberData(nameof(GetSignatureSizeBitsTestData))]
    public void GetSignatureSizeBytes_Valid(int keySizeBits)
    {
        const string code = nameof(code);

        var expected = (keySizeBits + 7) >> 3;
        var algorithm = new RsaSignatureAlgorithm(code, default, null!);
        var result = algorithm.GetSignatureSizeBytes(keySizeBits);
        Assert.Equal(expected, result);
    }

    public static IEnumerable<object[]> GetRoundTripTestData()
    {
        var keyBitSizes = new[] { 2048, 2048 + 64, 3072, 3072 + 128, 4096, 4096 + 192 };
        var hashes = new[] { HashAlgorithmName.SHA256, HashAlgorithmName.SHA384, HashAlgorithmName.SHA512 };
        var paddings = new[] { RSASignaturePadding.Pkcs1, RSASignaturePadding.Pss };

        foreach (var keyBitSize in keyBitSizes)
        foreach (var hash in hashes)
        foreach (var padding in paddings)
        {
            yield return new object[] { keyBitSize, hash, padding };
        }
    }

    [Theory]
    [MemberData(nameof(GetRoundTripTestData))]
    public void RoundTrip_Valid(int keySizeBits, HashAlgorithmName hashAlgorithmName, RSASignaturePadding padding)
    {
        const string keyId = nameof(keyId);
        const string code = nameof(code);

        var metadata = new KeyMetadata { KeyId = keyId };
        using var key = RSA.Create(keySizeBits);
        using var secretKey = SecretKeyFactory.CreateRsa(metadata, key);

        var algorithm = new RsaSignatureAlgorithm(code, hashAlgorithmName, padding);

        var dataSizeBytes = Random.Shared.Next(128, 1024);
        var signatureSizeBytes = algorithm.GetSignatureSizeBytes(keySizeBits);

        Span<byte> inputData = stackalloc byte[dataSizeBytes];
        Span<byte> signature = stackalloc byte[signatureSizeBytes];

        RandomNumberGenerator.Fill(inputData);

        var signResult = algorithm.TrySign(secretKey, inputData, signature, out var signBytesWritten);
        Assert.True(signResult);
        Assert.Equal(signatureSizeBytes, signBytesWritten);

        var verifyResult = algorithm.Verify(secretKey, inputData, signature);
        Assert.True(verifyResult);

        var controlAlgorithm = GetControlAlgorithm(hashAlgorithmName, padding);
        var controlHash = controlAlgorithm.Sign(inputData.ToArray(), key);
        Assert.Equal(signatureSizeBytes, controlHash.Length);

        var verifyHashFromControl = algorithm.Verify(secretKey, inputData, controlHash);
        Assert.True(verifyHashFromControl);

        var verifyHashUsingControl = controlAlgorithm.Verify(signature.ToArray(), inputData.ToArray(), key);
        Assert.True(verifyHashUsingControl);
    }

    private static global::Jose.IJwsAlgorithm GetControlAlgorithm(HashAlgorithmName hashAlgorithmName, RSASignaturePadding padding)
    {
        var hashSizeBits = hashAlgorithmName.GetHashSizeBits();
        if (padding == RSASignaturePadding.Pkcs1)
            return new global::Jose.RsaUsingSha($"SHA{hashSizeBits}");
        if (padding == RSASignaturePadding.Pss)
            return new global::Jose.RsaPssUsingSha((hashSizeBits + 7) >> 3);
        throw new InvalidOperationException();
    }

    public static IEnumerable<object[]> GetTryHashTestData()
    {
        yield return new object[] { 256, (HashFunctionDelegate)SHA256.TryHashData };
        yield return new object[] { 384, (HashFunctionDelegate)SHA384.TryHashData };
        yield return new object[] { 512, (HashFunctionDelegate)SHA512.TryHashData };
    }
}
