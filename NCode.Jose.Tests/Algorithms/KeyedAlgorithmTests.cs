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
using NCode.Jose.SecretKeys;

namespace NCode.Jose.Tests.Algorithms;

public class KeyedAlgorithmTests : BaseTests
{
    private Mock<KeyedAlgorithm> MockKeyedAlgorithm { get; }
    private KeyedAlgorithm Algorithm { get; }

    public KeyedAlgorithmTests()
    {
        MockKeyedAlgorithm = CreatePartialMock<KeyedAlgorithm>();
        Algorithm = MockKeyedAlgorithm.Object;
    }

    [Fact]
    public void ValidateSecretKey_Valid()
    {
        const string keyId = nameof(keyId);
        const int keySizeBytes = 32;
        const int keySizeBits = keySizeBytes * 8;

        Span<byte> key = new byte[keySizeBytes];
        using var secretKey = new SymmetricSecretKey(keyId, Array.Empty<string>(), key);

        MockKeyedAlgorithm
            .Setup(x => x.KeyBitSizes)
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
        using var secretKey = new SymmetricSecretKey(keyId, Array.Empty<string>(), key);

        MockKeyedAlgorithm
            .Setup(x => x.KeyBitSizes)
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
        using var secretKey = new SymmetricSecretKey(keyId, Array.Empty<string>(), key);

        MockKeyedAlgorithm
            .Setup(x => x.KeyBitSizes)
            .Returns(new[] { new KeySizes(keySizeBits + 8, keySizeBits + 8, 0) })
            .Verifiable();

        var exception = Assert.Throws<ArgumentException>(() =>
            Algorithm.ValidateSecretKey<SymmetricSecretKey>(secretKey));

        Assert.Equal("The secret key does not have a valid size for this cryptographic algorithm. (Parameter 'secretKey')", exception.Message);
    }
}
