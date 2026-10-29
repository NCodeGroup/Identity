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
using NCode.Identity.OpenId.Client.Http;
using NCode.Identity.OpenId.Client.Tokens;
using Xunit;

namespace NCode.Identity.OpenId.Client.Tests.Tokens;

public class ProtocolErrorReaderTests
{
    #region TryRead Tests

    [Fact]
    public void TryRead_WithNonStandardStatusCode_PreservesTheStatusCode()
    {
        const int statusCode = 499;
        var response = new ProviderHttpResponse(
            statusCode,
            JsonDocument.Parse("""{"error":"x"}""").RootElement
        );

        var result = ProtocolErrorReader.TryRead(response, out var error);

        Assert.True(result);
        Assert.NotNull(error);
        Assert.Equal(statusCode, error.StatusCode);
    }

    #endregion

    #region TryReadChallenge Tests

    private static ProviderHttpResponse CreateChallenge(string? challenge) =>
        new(401, null) { WwwAuthenticate = challenge };

    [Theory]
    [InlineData(
        "Bearer error=\"invalid_token\", error_description=\"The token expired\"",
        "invalid_token",
        "The token expired"
    )]
    [InlineData(
        "Bearer realm=\"op\",error_description=\"error=\\\"forged\\\"\",error=\"insufficient_scope\"",
        "insufficient_scope",
        "error=\"forged\""
    )]
    [InlineData("DPoP algs=\"ES256 PS256\", error=use_dpop_nonce", "use_dpop_nonce", null)]
    [InlineData(
        "Bearer realm=\"op\", DPoP error=\"invalid_dpop_proof\"",
        "invalid_dpop_proof",
        null
    )]
    public void TryReadChallenge_WithAnErrorParameter_ReadsIt(
        string challenge,
        string expectedError,
        string? expectedDescription
    )
    {
        var result = ProtocolErrorReader.TryReadChallenge(
            CreateChallenge(challenge),
            out var error
        );

        Assert.True(result);
        Assert.NotNull(error);
        Assert.Equal(expectedError, error.Error);
        Assert.Equal(expectedDescription, error.ErrorDescription);
        Assert.Equal(401, error.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Bearer realm=\"op\"")]
    [InlineData("Bearer error_description=\"no code\"")]
    public void TryReadChallenge_WithoutAnErrorParameter_ReadsNothing(string? challenge)
    {
        var result = ProtocolErrorReader.TryReadChallenge(
            CreateChallenge(challenge),
            out var error
        );

        Assert.False(result);
        Assert.Null(error);
    }

    #endregion
}
