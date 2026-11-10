// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.Extensions.Primitives;
using NCode.Identity.OpenId.Serialization;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Serialization;

public class StringValuesJsonConverterTests
{
    private JsonSerializerOptions Options { get; } =
        new() { Converters = { new StringValuesJsonConverter() } };

    #region Write Tests

    [Fact]
    public void Write_WhenEmpty_WritesNull()
    {
        var json = JsonSerializer.Serialize(StringValues.Empty, Options);

        Assert.Equal("null", json);
    }

    [Fact]
    public void Write_WhenSingle_WritesString()
    {
        var json = JsonSerializer.Serialize(new StringValues("one"), Options);

        Assert.Equal("\"one\"", json);
    }

    [Fact]
    public void Write_WhenMultiple_WritesJoinedString()
    {
        var json = JsonSerializer.Serialize(new StringValues(["a", "b", "c"]), Options);

        Assert.Equal(
            $"\"a{OpenIdProtocol.ParameterSeparatorChar}b{OpenIdProtocol.ParameterSeparatorChar}c\"",
            json
        );
    }

    #endregion

    #region Read Tests

    [Fact]
    public void Read_WhenNull_ReturnsEmpty()
    {
        var result = JsonSerializer.Deserialize<StringValues>("null", Options);

        Assert.Equal(StringValues.Empty, result);
    }

    [Fact]
    public void Read_WhenEmptyString_ReturnsEmpty()
    {
        var result = JsonSerializer.Deserialize<StringValues>("\"\"", Options);

        Assert.Equal(StringValues.Empty, result);
    }

    [Fact]
    public void Read_WhenSingle_ReturnsSingle()
    {
        var result = JsonSerializer.Deserialize<StringValues>("\"one\"", Options);

        Assert.Equal(new StringValues("one"), result);
    }

    [Fact]
    public void RoundTrip_WhenMultiple_PreservesValues()
    {
        var original = new StringValues(["a", "b", "c"]);

        var json = JsonSerializer.Serialize(original, Options);
        var result = JsonSerializer.Deserialize<StringValues>(json, Options);

        Assert.Equal(original, result);
    }

    #endregion
}
