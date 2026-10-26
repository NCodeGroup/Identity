#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using NCode.Buffers;
using NCode.Encoders;
using NCode.Identity.OpenId.Authentication.SharedState;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.SharedState;

public sealed class ExpiringKeysTests
{
    private const int DigestLength = 43;

    #region Create Tests

    [Fact]
    public void Create_PrefixesThePurposeAndTheTenantHashTagToTheDigest()
    {
        var key = ExpiringKeys.Create("purpose", "tenant-1", "a", "b");

        Assert.StartsWith("purpose:{tenant-1}:", key, StringComparison.Ordinal);
        Assert.Equal("purpose:{tenant-1}:".Length + DigestLength, key.Length);
    }

    [Fact]
    public void Create_DigestsTheLengthPrefixedUtf8OfEachComponent()
    {
        const string Component = "caf\u00e9";
        var bytes = SecureEncoding.UTF8.GetBytes(Component);
        var input = new byte[sizeof(int) + bytes.Length];
        BinaryPrimitives.WriteInt32BigEndian(input, bytes.Length);
        bytes.CopyTo(input, sizeof(int));

        var key = ExpiringKeys.Create("purpose", "tenant-1", Component);

        Assert.Equal($"purpose:{{tenant-1}}:{Base64Url.Encode(SHA256.HashData(input))}", key);
    }

    [Fact]
    public void Create_WhenInputsAreEqual_IsDeterministic()
    {
        var first = ExpiringKeys.Create("purpose", "tenant-1", "a", "b");
        var second = ExpiringKeys.Create("purpose", "tenant-1", "a", "b");

        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData("a:b", "c", "a", "b:c")]
    [InlineData("ab", "c", "a", "bc")]
    [InlineData("", "ab", "ab", "")]
    public void Create_WhenComponentBoundariesDiffer_ProducesDistinctKeys(
        string firstA,
        string firstB,
        string secondA,
        string secondB
    )
    {
        var first = ExpiringKeys.Create("purpose", "tenant-1", firstA, firstB);
        var second = ExpiringKeys.Create("purpose", "tenant-1", secondA, secondB);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Create_WhenTenantsDiffer_ProducesDistinctKeys()
    {
        var first = ExpiringKeys.Create("purpose", "tenant-1", "a");
        var second = ExpiringKeys.Create("purpose", "tenant-2", "a");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Create_WhenPurposesDiffer_ProducesDistinctKeys()
    {
        var first = ExpiringKeys.Create("purpose-1", "tenant-1", "a");
        var second = ExpiringKeys.Create("purpose-2", "tenant-1", "a");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Create_WhenComponentHasLoneSurrogate_Throws()
    {
        Assert.Throws<EncoderFallbackException>(() =>
            ExpiringKeys.Create("purpose", "tenant-1", "a\uD800")
        );
    }

    [Fact]
    public void Create_WhenAComponentIsLong_BoundsTheKeyLength()
    {
        var key = ExpiringKeys.Create("purpose", "tenant-1", new string('x', 100_000));

        Assert.Equal("purpose:{tenant-1}:".Length + DigestLength, key.Length);
    }

    #endregion
}
