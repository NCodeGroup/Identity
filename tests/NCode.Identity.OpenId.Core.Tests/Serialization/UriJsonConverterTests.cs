// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NCode.Json;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Serialization;

public class UriJsonConverterTests
{
    private readonly JsonSerializerOptions _options = new()
    {
        Converters = { new UriJsonConverter() },
    };

    #region Round-Trip Tests

    [Fact]
    public void RoundTrip_WhenAbsoluteUri_PreservesValue()
    {
        var original = new Uri("https://example.com/path?query=1");

        var json = JsonSerializer.Serialize(original, _options);
        var result = JsonSerializer.Deserialize<Uri>(json, _options);

        Assert.NotNull(result);
        Assert.True(result.IsAbsoluteUri);
        Assert.Equal(original, result);
    }

    [Fact]
    public void RoundTrip_WhenRelativeUri_PreservesValue()
    {
        var original = new Uri("/relative/path", UriKind.Relative);

        var json = JsonSerializer.Serialize(original, _options);
        var result = JsonSerializer.Deserialize<Uri>(json, _options);

        Assert.NotNull(result);
        Assert.False(result.IsAbsoluteUri);
        Assert.Equal(original.ToString(), result.ToString());
    }

    #endregion

    #region Null Tests

    [Fact]
    public void Write_WhenNull_WritesNull()
    {
        var json = JsonSerializer.Serialize<Uri?>(null, _options);

        Assert.Equal("null", json);
    }

    [Fact]
    public void Read_WhenNull_ReturnsNull()
    {
        var result = JsonSerializer.Deserialize<Uri?>("null", _options);

        Assert.Null(result);
    }

    #endregion
}
