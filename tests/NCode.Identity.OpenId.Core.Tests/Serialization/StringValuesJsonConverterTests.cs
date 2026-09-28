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

using System.Text.Json;
using Microsoft.Extensions.Primitives;
using NCode.Identity.OpenId.Serialization;
using Xunit;

namespace NCode.Identity.OpenId.Tests.Serialization;

public class StringValuesJsonConverterTests
{
    private readonly JsonSerializerOptions _options = new()
    {
        Converters = { new StringValuesJsonConverter() },
    };

    #region Write Tests

    [Fact]
    public void Write_WhenEmpty_WritesNull()
    {
        var json = JsonSerializer.Serialize(StringValues.Empty, _options);

        Assert.Equal("null", json);
    }

    [Fact]
    public void Write_WhenSingle_WritesString()
    {
        var json = JsonSerializer.Serialize(new StringValues("one"), _options);

        Assert.Equal("\"one\"", json);
    }

    [Fact]
    public void Write_WhenMultiple_WritesJoinedString()
    {
        var json = JsonSerializer.Serialize(new StringValues(["a", "b", "c"]), _options);

        Assert.Equal(
            $"\"a{OpenIdConstants.ParameterSeparatorChar}b{OpenIdConstants.ParameterSeparatorChar}c\"",
            json
        );
    }

    #endregion

    #region Read Tests

    [Fact]
    public void Read_WhenNull_ReturnsEmpty()
    {
        var result = JsonSerializer.Deserialize<StringValues>("null", _options);

        Assert.Equal(StringValues.Empty, result);
    }

    [Fact]
    public void Read_WhenEmptyString_ReturnsEmpty()
    {
        var result = JsonSerializer.Deserialize<StringValues>("\"\"", _options);

        Assert.Equal(StringValues.Empty, result);
    }

    [Fact]
    public void Read_WhenSingle_ReturnsSingle()
    {
        var result = JsonSerializer.Deserialize<StringValues>("\"one\"", _options);

        Assert.Equal(new StringValues("one"), result);
    }

    [Fact]
    public void RoundTrip_WhenMultiple_PreservesValues()
    {
        var original = new StringValues(["a", "b", "c"]);

        var json = JsonSerializer.Serialize(original, _options);
        var result = JsonSerializer.Deserialize<StringValues>(json, _options);

        Assert.Equal(original, result);
    }

    #endregion
}
