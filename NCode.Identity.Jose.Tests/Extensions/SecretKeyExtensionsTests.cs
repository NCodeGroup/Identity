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
using NCode.Identity.DataProtection;
using NCode.Identity.Secrets;

namespace NCode.Jose.Tests.Extensions;

public class SecretKeyExtensionsTests : BaseTests
{
    private DefaultSecretKeyFactory SecretKeyFactory { get; } = new(NoneSecureDataProtector.Singleton);

    [Fact]
    public void Validate_Valid()
    {
        const int keySizeBytes = 32;
        const int keySizeBits = keySizeBytes * 8;

        Span<byte> key = new byte[keySizeBytes];
        var secretKey = SecretKeyFactory.CreateSymmetric(default, key);

        var legalKeyBitSizes = new[] { new KeySizes(keySizeBits, keySizeBits, 0) };

        var result = secretKey.Validate<SymmetricSecretKey>(legalKeyBitSizes);
        Assert.Same(secretKey, result);
    }

    [Fact]
    public void Validate_InvalidType()
    {
        const int keySizeBytes = 32;
        const int keySizeBits = keySizeBytes * 8;

        Span<byte> key = new byte[keySizeBytes];
        var secretKey = SecretKeyFactory.CreateSymmetric(default, key);

        var legalKeyBitSizes = new[] { new KeySizes(keySizeBits, keySizeBits, 0) };

        var exception = Assert.Throws<ArgumentException>(() =>
            secretKey.Validate<AsymmetricSecretKey>(legalKeyBitSizes));

        Assert.Equal($"The secret key was expected to be a type of '{typeof(AsymmetricSecretKey).FullName}', but '{secretKey.GetType().FullName}' was given instead. (Parameter 'secretKey')", exception.Message);
    }

    [Fact]
    public void Validate_InvalidSize()
    {
        const int keySizeBytes = 32;
        const int keySizeBits = keySizeBytes * 8;

        Span<byte> key = new byte[keySizeBytes];
        var secretKey = SecretKeyFactory.CreateSymmetric(default, key);

        var legalKeyBitSizes = new[] { new KeySizes(keySizeBits + 8, keySizeBits + 8, 0) };

        var exception = Assert.Throws<ArgumentException>(() =>
            secretKey.Validate<SymmetricSecretKey>(legalKeyBitSizes));

        Assert.Equal("The secret key does not have a valid size for this cryptographic algorithm. (Parameter 'secretKey')", exception.Message);
    }
}
