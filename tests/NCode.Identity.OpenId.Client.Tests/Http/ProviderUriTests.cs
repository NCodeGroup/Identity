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

using NCode.Identity.OpenId.Client.Http;
using Xunit;

namespace NCode.Identity.OpenId.Client.Tests.Http;

public class ProviderUriTests : BaseTests
{
    #region TryParseEndpoint Tests

    [Theory]
    [InlineData("https://op.example.test")]
    [InlineData("https://op.example.test/t1")]
    [InlineData("http://localhost:5000")]
    [InlineData("http://127.0.0.1/x")]
    [InlineData("http://[::1]/")]
    public void TryParseEndpoint_WithAcceptedValue_ReturnsTrue(string value)
    {
        var result = ProviderUri.TryParseEndpoint(value, out var uri);

        Assert.True(result);
        Assert.NotNull(uri);
    }

    [Theory]
    [InlineData("http://op.example.test")]
    [InlineData("ftp://op.example.test")]
    [InlineData("/relative")]
    [InlineData(@"C:\path")]
    [InlineData(" https://op.example.test")]
    [InlineData("https://op.example.test/#f")]
    [InlineData("https://")]
    [InlineData("")]
    [InlineData(null)]
    public void TryParseEndpoint_WithRejectedValue_ReturnsFalse(string? value)
    {
        var result = ProviderUri.TryParseEndpoint(value, out var uri);

        Assert.False(result);
        Assert.Null(uri);
    }

    #endregion
}
