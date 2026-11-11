// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace NCode.Identity.Json.Tests;

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
