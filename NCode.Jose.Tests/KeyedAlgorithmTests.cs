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

namespace NCode.Jose.Tests;

public class KeyedAlgorithmTests : BaseTests
{
    private Mock<KeyedAlgorithm> MockKeyedAlgorithm { get; }
    private KeyedAlgorithm Algorithm { get; }

    public KeyedAlgorithmTests()
    {
        MockKeyedAlgorithm = CreatePartialMock<KeyedAlgorithm>();
        Algorithm = MockKeyedAlgorithm.Object;
    }

    public static IEnumerable<object?[]> GetHashSizeBitsFromAlgorithmNameTestData()
    {
        yield return new object?[] { HashAlgorithmName.MD5, null };
        yield return new object?[] { HashAlgorithmName.SHA1, 160 };
        yield return new object?[] { HashAlgorithmName.SHA256, 256 };
        yield return new object?[] { HashAlgorithmName.SHA384, 384 };
        yield return new object?[] { HashAlgorithmName.SHA512, 512 };
    }

    [Theory]
    [MemberData(nameof(GetHashSizeBitsFromAlgorithmNameTestData))]
    public void HashSizeBitsFromAlgorithmName_Valid(HashAlgorithmName hashAlgorithmName, int? expected)
    {
        if (expected.HasValue)
        {
            var actual = KeyedAlgorithm.HashSizeBitsFromAlgorithmName(hashAlgorithmName);
            Assert.Equal(expected.Value, actual);
        }
        else
        {
            var exception = Assert.Throws<ArgumentException>(() =>
                KeyedAlgorithm.HashSizeBitsFromAlgorithmName(hashAlgorithmName));

            Assert.Equal("Unsupported hash algorithm. (Parameter 'hashAlgorithmName')", exception.Message);
        }
    }

    [Fact]
    public void ValidateSecretKey_Valid()
    {
        const string keyId = nameof(keyId);
        const int keySizeBytes = 32;
        const int keySizeBits = keySizeBytes * 8;

        Span<byte> key = new byte[keySizeBytes];
        using var secretKey = new SymmetricSecretKey(keyId, key);

        MockKeyedAlgorithm
            .Setup(_ => _.KeyBitSizes)
            .Returns(new[] { new KeySizes(keySizeBits, keySizeBits, 0) })
            .Verifiable();

        var result = Algorithm.ValidateSecretKey<SymmetricSecretKey>(secretKey);
        Assert.Same(secretKey, result);
    }

    [Fact]
    public void ValidateSecretKey_InvalidType()
    {
        const string keyId = nameof(keyId);
        const int keySizeBytes = 32;
        const int keySizeBits = keySizeBytes * 8;

        Span<byte> key = new byte[keySizeBytes];
        using var secretKey = new SymmetricSecretKey(keyId, key);

        MockKeyedAlgorithm
            .Setup(_ => _.KeyBitSizes)
            .Returns(new[] { new KeySizes(keySizeBits, keySizeBits, 0) })
            .Verifiable();

        var exception = Assert.Throws<ArgumentException>(() =>
            Algorithm.ValidateSecretKey<AsymmetricSecretKey>(secretKey));

        Assert.Equal($"The secret key was expected to be a type of '{typeof(AsymmetricSecretKey).FullName}', but '{secretKey.GetType().FullName}' was given instead. (Parameter 'secretKey')", exception.Message);
    }

    [Fact]
    public void ValidateSecretKey_InvalidSize()
    {
        const string keyId = nameof(keyId);
        const int keySizeBytes = 32;
        const int keySizeBits = keySizeBytes * 8;

        Span<byte> key = new byte[keySizeBytes];
        using var secretKey = new SymmetricSecretKey(keyId, key);

        MockKeyedAlgorithm
            .Setup(_ => _.KeyBitSizes)
            .Returns(new[] { new KeySizes(keySizeBits + 8, keySizeBits + 8, 0) })
            .Verifiable();

        var exception = Assert.Throws<ArgumentException>(() =>
            Algorithm.ValidateSecretKey<SymmetricSecretKey>(secretKey));

        Assert.Equal("The secret key does not have a valid size for this cryptographic algorithm. (Parameter 'secretKey')", exception.Message);
    }
}
