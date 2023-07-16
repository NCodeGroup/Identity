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

using System.Buffers;
using System.Buffers.Binary;
using NIdentity.OpenId.Cryptography;
using NIdentity.OpenId.Cryptography.Binary;
using Xunit;

namespace NIdentity.OpenId.Core.Tests.Cryptography;

public class BinaryUtilityTests
{
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 0, 1)]
    [InlineData(0, 1, 1)]
    [InlineData(1, 1, 0)]
    [InlineData(0b1010, 0b0101, 0b1111)]
    [InlineData(0b101000001010, 0b010100000101, 0b111100001111)]
    public void Xor_Valid(long x, long y, long expected)
    {
        Span<byte> xBuffer = stackalloc byte[sizeof(long)];
        Span<byte> destination = stackalloc byte[sizeof(long)];

        BinaryPrimitives.WriteInt64BigEndian(xBuffer, x);

        BinaryUtility.Xor(xBuffer, y, destination);

        var result = BinaryPrimitives.ReadInt64BigEndian(destination);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Xor_GivenLargeBufferForX_ThenValid()
    {
        Span<byte> xBuffer = stackalloc byte[sizeof(long) + 1];
        Span<byte> destination = stackalloc byte[sizeof(long)];

        BinaryUtility.Xor(xBuffer, 0, destination);
    }

    [Fact]
    public void Xor_GivenLargeBufferForDestination_ThenValid()
    {
        Span<byte> xBuffer = stackalloc byte[sizeof(long)];
        Span<byte> destination = stackalloc byte[sizeof(long) + 1];

        BinaryUtility.Xor(xBuffer, 0, destination);
    }

    [Fact]
    public void Xor_GivenTooSmallBufferForX_ThenThrows()
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            Span<byte> xBuffer = stackalloc byte[sizeof(long) - 1];
            Span<byte> destination = stackalloc byte[sizeof(long)];

            BinaryUtility.Xor(xBuffer, 0, destination);
        });
    }

    [Fact]
    public void Xor_GivenTooSmallBufferForDestination_ThenThrows()
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            Span<byte> xBuffer = stackalloc byte[sizeof(long)];
            Span<byte> destination = stackalloc byte[sizeof(long) - 1];

            BinaryUtility.Xor(xBuffer, 0, destination);
        });
    }

    [Fact]
    public void Concat_GivenPrepend_ThenValid()
    {
        ReadOnlyMemory<byte> Allocate(int cb) => new byte[cb].AsMemory();

        var sizes = new[] { 8, 16, 32, 64, 128 };
        Span<byte> prepend = stackalloc byte[sizes[0]];
        var buffers = sizes.Skip(1).Select(Allocate);

        var result = BinaryUtility.Concat(prepend, buffers);
        Assert.False(result.IsSingleSegment);
        Assert.Equal(sizes.Sum(), result.Length);

        var index = 0;
        var enumerator = result.GetEnumerator();
        while (enumerator.MoveNext())
        {
            Assert.Equal(sizes[index++], enumerator.Current.Length);
        }
    }

    [Fact]
    public void Concat_GivenEnumerableWithEmpty_ThenValid()
    {
        var result = BinaryUtility.Concat(Enumerable.Empty<ReadOnlyMemory<byte>>());
        Assert.Equal(ReadOnlySequence<byte>.Empty, result);
    }

    [Fact]
    public void Concat_GivenEnumerableWithSingle_ThenValid()
    {
        var buffer = new byte[32];
        Random.Shared.NextBytes(buffer);

        var single = new ReadOnlyMemory<byte>(buffer);
        var array = new[] { single };

        var result = BinaryUtility.Concat(array.AsEnumerable());
        Assert.True(result.IsSingleSegment);

        var areEqual = single.Span.SequenceEqual(result.First.Span);
        Assert.True(areEqual);
    }

    [Fact]
    public void Concat_GivenEnumerableWithMultiple_ThenValid()
    {
        ReadOnlyMemory<byte> Allocate(int cb) => new byte[cb].AsMemory();

        var sizes = new[] { 8, 16, 32, 64, 128 };
        var buffers = sizes.Select(Allocate).ToArray();

        var result = BinaryUtility.Concat(buffers.AsEnumerable());
        Assert.False(result.IsSingleSegment);
        Assert.Equal(sizes.Sum(), result.Length);

        var index = 0;
        var enumerator = result.GetEnumerator();
        while (enumerator.MoveNext())
        {
            Assert.Equal(sizes[index++], enumerator.Current.Length);
        }
    }

    [Fact]
    public void Concat_GivenArrayWithEmpty_ThenValid()
    {
        var result = BinaryUtility.Concat(Array.Empty<ReadOnlyMemory<byte>>());
        Assert.Equal(ReadOnlySequence<byte>.Empty, result);
    }

    [Fact]
    public void Concat_GivenArrayWithSingle_ThenValid()
    {
        var buffer = new byte[32];
        Random.Shared.NextBytes(buffer);

        var single = new ReadOnlyMemory<byte>(buffer);
        var array = new[] { single };

        var result = BinaryUtility.Concat(array);
        Assert.True(result.IsSingleSegment);

        var areEqual = single.Span.SequenceEqual(result.First.Span);
        Assert.True(areEqual);
    }

    [Fact]
    public void Concat_GivenArrayWithMultiple_ThenValid()
    {
        ReadOnlyMemory<byte> Allocate(int cb) => new byte[cb].AsMemory();

        var sizes = new[] { 8, 16, 32, 64, 128 };
        var buffers = sizes.Select(Allocate).ToArray();

        var result = BinaryUtility.Concat(buffers);
        Assert.False(result.IsSingleSegment);
        Assert.Equal(sizes.Sum(), result.Length);

        var index = 0;
        var enumerator = result.GetEnumerator();
        while (enumerator.MoveNext())
        {
            Assert.Equal(sizes[index++], enumerator.Current.Length);
        }
    }

    [Fact]
    public void Concat_GivenSpans_ThenValid()
    {
        Span<byte> a = stackalloc byte[3];
        Random.Shared.NextBytes(a);

        Span<byte> b = stackalloc byte[5];
        Random.Shared.NextBytes(b);

        Span<byte> destination = stackalloc byte[a.Length + b.Length];
        BinaryUtility.Concat(a, b, destination);

        var isFirstPartEqual = destination[..a.Length].SequenceEqual(a);
        Assert.True(isFirstPartEqual);

        var isSecondPartEqual = destination[a.Length..].SequenceEqual(b);
        Assert.True(isSecondPartEqual);
    }

    [Fact]
    public void Concat_GivenSpans_WhenTooSmallDestination_ThenThrows()
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            Span<byte> a = stackalloc byte[3];
            Span<byte> b = stackalloc byte[5];
            Span<byte> destination = stackalloc byte[a.Length + b.Length - 1];
            BinaryUtility.Concat(a, b, destination);
        });
    }

    [Fact]
    public void Concat_GivenSpans_WhenLargerDestination_ThenValid()
    {
        Span<byte> a = stackalloc byte[3];
        Span<byte> b = stackalloc byte[5];
        Span<byte> destination = stackalloc byte[a.Length + b.Length + 1];
        BinaryUtility.Concat(a, b, destination);
    }

    [Fact]
    public void Concat_GivenThreeSpansAndInt64_ThenValid()
    {
        Span<byte> a = stackalloc byte[3];
        Random.Shared.NextBytes(a);

        Span<byte> b = stackalloc byte[5];
        Random.Shared.NextBytes(b);

        Span<byte> c = stackalloc byte[8];
        Random.Shared.NextBytes(c);

        const long d = 0b101000001010;

        Span<byte> destination = stackalloc byte[a.Length + b.Length + c.Length + sizeof(long)];
        BinaryUtility.Concat(a, b, c, d, destination);

        var pos = destination;

        var isAEqual = pos[..a.Length].SequenceEqual(a);
        Assert.True(isAEqual);
        pos = pos[a.Length..];

        var isBEqual = pos[..b.Length].SequenceEqual(b);
        Assert.True(isBEqual);
        pos = pos[b.Length..];

        var isCEqual = pos[..c.Length].SequenceEqual(c);
        Assert.True(isCEqual);
        pos = pos[c.Length..];

        Assert.Equal(d, BinaryPrimitives.ReadInt64BigEndian(pos));
    }

    [Fact]
    public void Concat_GivenThreeSpansAndInt64_WhenTooSmallDestination_ThenThrows()
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            Span<byte> a = stackalloc byte[3];
            Span<byte> b = stackalloc byte[5];
            Span<byte> c = stackalloc byte[8];
            const long d = 0b101000001010;
            Span<byte> destination = stackalloc byte[a.Length + b.Length + c.Length + sizeof(long) - 1];
            BinaryUtility.Concat(a, b, c, d, destination);
        });
    }

    [Fact]
    public void Concat_GivenThreeSpansAndInt64_WhenLargerDestination_ThenValid()
    {
        Span<byte> a = stackalloc byte[3];
        Span<byte> b = stackalloc byte[5];
        Span<byte> c = stackalloc byte[8];
        const long d = 0b101000001010;
        Span<byte> destination = stackalloc byte[a.Length + b.Length + c.Length + sizeof(long) + 1];
        BinaryUtility.Concat(a, b, c, d, destination);
    }
}
