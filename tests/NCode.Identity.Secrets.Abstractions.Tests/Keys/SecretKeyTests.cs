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

namespace NCode.Identity.Secrets.Keys;

public class SecretKeyTests
{
    #region KeySizeBytes Property Tests

    [Theory]
    [InlineData(256, 32)]
    [InlineData(255, 32)]
    [InlineData(8, 1)]
    [InlineData(1, 1)]
    [InlineData(0, 0)]
    public void KeySizeBytes_WhenGivenBits_RoundsUp(int bits, int expectedBytes)
    {
        var key = new TestSecretKey(new KeyMetadata(), keySizeBits: bits);

        Assert.Equal(expectedBytes, key.KeySizeBytes);
    }

    #endregion

    #region KeyId Property Tests

    [Fact]
    public void KeyId_WhenMetadataHasKeyId_ReturnsKeyId()
    {
        var key = new TestSecretKey(new KeyMetadata { KeyId = "abc" });

        Assert.Equal("abc", key.KeyId);
    }

    [Fact]
    public void KeyId_WhenMetadataHasNoKeyId_ReturnsNull()
    {
        var key = new TestSecretKey(new KeyMetadata());

        Assert.Null(key.KeyId);
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_WhenKeyIdPresent_IncludesQuotedKeyId()
    {
        var key = new TestSecretKey(
            new KeyMetadata { KeyId = "abc" },
            keySizeBits: 128,
            keyType: "oct"
        );

        Assert.Equal("oct { KeyId = 'abc', Size = 128 }", key.ToString());
    }

    [Fact]
    public void ToString_WhenKeyIdNull_ShowsNullPlaceholder()
    {
        var key = new TestSecretKey(new KeyMetadata(), keySizeBits: 128, keyType: "oct");

        Assert.Equal("oct { KeyId = (null), Size = 128 }", key.ToString());
    }

    [Fact]
    public void ToString_WhenCalledTwice_ReturnsCachedValue()
    {
        var key = new TestSecretKey(new KeyMetadata { KeyId = "abc" });

        var first = key.ToString();
        var second = key.ToString();

        Assert.Same(first, second);
    }

    #endregion

    #region KeyType Property Tests

    [Fact]
    public void KeyType_WhenSet_RoundTrips()
    {
        var key = new TestSecretKey(new KeyMetadata(), keyType: "custom");

        Assert.Equal("custom", key.KeyType);
    }

    #endregion
}
