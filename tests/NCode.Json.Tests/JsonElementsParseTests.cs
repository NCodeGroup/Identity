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

using System.Buffers;
using System.Text.Json;
using NCode.Buffers;

namespace NCode.Json.Tests;

public class JsonElementsParseTests
{
    private sealed class Segment : ReadOnlySequenceSegment<byte>
    {
        public Segment(ReadOnlyMemory<byte> memory) => Memory = memory;

        public Segment Append(ReadOnlyMemory<byte> memory)
        {
            var next = new Segment(memory) { RunningIndex = RunningIndex + Memory.Length };
            Next = next;
            return next;
        }
    }

    // Splits the UTF-8 text into one segment per chunk, so a value straddles segment boundaries.
    private static ReadOnlySequence<byte> CreateSequence(string json, int chunkSize)
    {
        var bytes = SecureEncoding.UTF8.GetBytes(json);
        var first = new Segment(bytes.AsMemory(0, Math.Min(chunkSize, bytes.Length)));
        var last = first;
        for (var offset = chunkSize; offset < bytes.Length; offset += chunkSize)
        {
            last = last.Append(bytes.AsMemory(offset, Math.Min(chunkSize, bytes.Length - offset)));
        }

        return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
    }

    #region Parse Tests

    [Fact]
    public void Parse_WithASingleSegment_ReturnsTheValue()
    {
        var sequence = new ReadOnlySequence<byte>("""{"a":1}"""u8.ToArray());

        var value = JsonElement.Parse(sequence);

        Assert.Equal(1, value.GetProperty("a").GetInt32());
    }

    [Fact]
    public void Parse_WithManySegments_ReturnsTheValue()
    {
        var sequence = CreateSequence("""{"keys":[{"kid":"k1"},{"kid":"k2"}]}""", chunkSize: 3);

        var value = JsonElement.Parse(sequence);

        Assert.Equal(
            ["k1", "k2"],
            value
                .GetProperty("keys")
                .EnumerateArray()
                .Select(key => key.GetProperty("kid").GetString())
        );
    }

    [Fact]
    public void Parse_WithTrailingWhitespace_ReturnsTheValue()
    {
        var sequence = CreateSequence("[1,2] \r\n", chunkSize: 2);

        var value = JsonElement.Parse(sequence);

        Assert.Equal(2, value.GetArrayLength());
    }

    [Theory]
    [InlineData("{} {}")]
    [InlineData("[1] x")]
    [InlineData("")]
    [InlineData("{")]
    public void Parse_WhenTheTextIsNotOneValue_Throws(string json)
    {
        var sequence = CreateSequence(json, chunkSize: 2);

        Assert.ThrowsAny<JsonException>(() => JsonElement.Parse(sequence));
    }

    [Fact]
    public void Parse_WithCommentsAllowedByTheOptions_SkipsThem()
    {
        var sequence = CreateSequence("/* note */ {\"a\":true}", chunkSize: 4);

        var value = JsonElement.Parse(
            sequence,
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip }
        );

        Assert.True(value.GetProperty("a").GetBoolean());
    }

    #endregion
}
