#region Copyright Preamble

//
//    Copyright @ 2025 NCode Group
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
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Secrets.Logic;

public class SecretKeyExtensionsTests
{
    private static readonly KeySizes[] LegalSizes = [new(minSize: 256, maxSize: 256, skipSize: 0)];

    #region Validate Tests

    [Fact]
    public void Validate_WhenTypeAndSizeValid_ReturnsTypedKey()
    {
        var key = new TestSecretKey(new KeyMetadata(), keySizeBits: 256);

        var result = key.Validate<TestSecretKey>(LegalSizes);

        Assert.Same(key, result);
    }

    [Fact]
    public void Validate_WhenWrongType_ThrowsArgumentException()
    {
        SecretKey key = new TestSecretKey(new KeyMetadata(), keySizeBits: 256);

        var exception = Assert.Throws<ArgumentException>(() =>
            key.Validate<OtherSecretKey>(LegalSizes)
        );

        Assert.Equal("secretKey", exception.ParamName);
    }

    [Fact]
    public void Validate_WhenIllegalSize_ThrowsArgumentException()
    {
        var key = new TestSecretKey(new KeyMetadata(), keySizeBits: 128);

        var exception = Assert.Throws<ArgumentException>(() =>
            key.Validate<TestSecretKey>(LegalSizes)
        );

        Assert.Equal("secretKey", exception.ParamName);
    }

    #endregion

    private sealed class OtherSecretKey : SecretKey
    {
        public override string KeyType => "other";
        public override KeyMetadata Metadata => new();
        public override int KeySizeBits => 256;
    }
}
