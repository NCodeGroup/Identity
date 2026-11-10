// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace NCode.Identity.OpenId.Core.Tests;

public class AbsoluteUriTests
{
    #region TryParse Tests

    [Theory]
    [InlineData("https://client.example/cb")]
    [InlineData("http://127.0.0.1:8080/cb")]
    [InlineData("com.example.app:/oauth2redirect")]
    [InlineData("urn:ietf:params:oauth:grant-type:device_code")]
    [InlineData("HTTPS://CLIENT.EXAMPLE/cb")]
    [InlineData("file:///etc/passwd")]
    public void TryParse_WhenTheTextNamesItsScheme_ReturnsTheUri(string value)
    {
        var parsed = AbsoluteUri.TryParse(value, out var uri);

        Assert.True(parsed);
        Assert.NotNull(uri);
        Assert.True(uri.IsAbsoluteUri);
    }

    [Theory]
    [InlineData("/signed-out")]
    [InlineData("/")]
    [InlineData(@"C:\callback")]
    [InlineData(@"\\server\share\cb")]
    [InlineData("callback")]
    [InlineData("//client.example/cb")]
    [InlineData(" https://client.example/cb")]
    [InlineData("https://client.example/cb ")]
    [InlineData("https://client .example/cb")]
    [InlineData("")]
    [InlineData(null)]
    public void TryParse_WhenTheTextIsNotAnAbsoluteUriWithAScheme_ReturnsFalse(string? value)
    {
        var parsed = AbsoluteUri.TryParse(value, out var uri);

        Assert.False(parsed);
        Assert.Null(uri);
    }

    #endregion

    #region TryParseHttps Tests

    [Theory]
    [InlineData("https://client.example/icon.png")]
    [InlineData("HTTPS://CLIENT.EXAMPLE/icon.png")]
    public void TryParseHttps_WhenTheTextIsAnHttpsUri_ReturnsTheUri(string value)
    {
        var parsed = AbsoluteUri.TryParseHttps(value, out var uri);

        Assert.True(parsed);
        Assert.NotNull(uri);
        Assert.Equal(Uri.UriSchemeHttps, uri.Scheme);
    }

    [Theory]
    [InlineData("http://client.example/icon.png")]
    [InlineData("/icon.png")]
    [InlineData(" https://client.example/icon.png")]
    [InlineData("https://client.example/icon .png")]
    [InlineData("javascript:alert(1)")]
    [InlineData("")]
    [InlineData(null)]
    public void TryParseHttps_WhenTheTextIsNotAnHttpsUri_ReturnsFalse(string? value)
    {
        var parsed = AbsoluteUri.TryParseHttps(value, out var uri);

        Assert.False(parsed);
        Assert.Null(uri);
    }

    #endregion
}
