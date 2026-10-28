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

using System.Text.Json;

namespace NCode.Json.Tests;

public class WholeInt32JsonConverterTests
{
    private static JsonSerializerOptions Options { get; } =
        new() { Converters = { new WholeInt32JsonConverter() } };

    #region Read Tests

    [Theory]
    [InlineData("60")]
    [InlineData("60.0")]
    [InlineData("6e1")]
    public void Read_WhenWholeNumber_ReturnsValue(string json)
    {
        var value = JsonSerializer.Deserialize<int>(json, Options);

        Assert.Equal(60, value);
    }

    [Fact]
    public void Read_WhenNullableAndNull_ReturnsNull()
    {
        var value = JsonSerializer.Deserialize<int?>("null", Options);

        Assert.Null(value);
    }

    [Theory]
    [InlineData("60.5")]
    [InlineData("\"60\"")]
    [InlineData("2147483648.0")]
    [InlineData("true")]
    public void Read_WhenNotAWholeInt32_ThrowsJsonException(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<int>(json, Options));
    }

    #endregion

    #region Write Tests

    [Fact]
    public void Write_WhenValue_WritesJsonInteger()
    {
        var json = JsonSerializer.Serialize(60, Options);

        Assert.Equal("60", json);
    }

    #endregion
}
