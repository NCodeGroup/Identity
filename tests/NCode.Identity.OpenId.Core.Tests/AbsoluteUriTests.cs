#region Copyright Preamble
//
//    Copyright @ 2026 NCode Group
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
}
