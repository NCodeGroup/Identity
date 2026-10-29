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
using Xunit;

namespace NCode.Identity.OpenId.Client.Tests.Tokens;

public class TokenResponseTests : BaseTests
{
    private const string Document =
        """{"access_token":"at","token_type":"N_A","issued_token_type":"urn:issued","expires_in":60,"custom":"value"}""";

    #region Deserialize Tests

    [Fact]
    public void Deserialize_WithIssuedTokenTypeAndUnknownMember_MapsBoth()
    {
        var response = JsonSerializer.Deserialize<TokenResponse>(Document);

        Assert.NotNull(response);
        Assert.Equal("urn:issued", response.IssuedTokenType);
        Assert.NotNull(response.ExtensionData);
        Assert.Equal("value", response.ExtensionData["custom"].GetString());
    }

    [Fact]
    public void Deserialize_WithExpiresInZeroFraction_ReadsWholeSeconds()
    {
        const string json = """{"access_token":"at","token_type":"Bearer","expires_in":60.0}""";

        var response = JsonSerializer.Deserialize<TokenResponse>(json);

        Assert.NotNull(response);
        Assert.Equal(60, response.ExpiresIn);
    }

    [Fact]
    public void Deserialize_WithExpiresInNonZeroFraction_ThrowsJsonException()
    {
        const string json = """{"access_token":"at","token_type":"Bearer","expires_in":60.5}""";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<TokenResponse>(json));
    }

    [Fact]
    public void Serialize_WithIssuedTokenTypeAndExtensionData_WritesBoth()
    {
        var response = JsonSerializer.Deserialize<TokenResponse>(Document);

        var json = JsonSerializer.Serialize(response);

        var document = JsonElement.Parse(json);
        Assert.Equal("urn:issued", document.GetProperty("issued_token_type").GetString());
        Assert.Equal("value", document.GetProperty("custom").GetString());
    }

    #endregion

    #region RefreshTokenExpiresIn Tests

    [Theory]
    [InlineData("2592000", 2_592_000)]
    [InlineData("2592000.0", 2_592_000)]
    public void RefreshTokenExpiresIn_WhenAPositiveWholeNumber_ReadsIt(string value, int expected)
    {
        var json =
            $$"""{"access_token":"at","token_type":"Bearer","refresh_token_expires_in":{{value}}}""";

        var response = JsonSerializer.Deserialize<TokenResponse>(json);

        Assert.NotNull(response);
        Assert.Equal(expected, response.RefreshTokenExpiresIn);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("60.5")]
    [InlineData("\"3600\"")]
    [InlineData("null")]
    public void RefreshTokenExpiresIn_WhenNotAPositiveWholeNumber_IgnoresItWithoutFailing(
        string value
    )
    {
        // The member is advisory, so a provider's malformed value never fails the token response.
        var json =
            $$"""{"access_token":"at","token_type":"Bearer","refresh_token_expires_in":{{value}}}""";

        var response = JsonSerializer.Deserialize<TokenResponse>(json);

        Assert.NotNull(response);
        Assert.Null(response.RefreshTokenExpiresIn);
    }

    [Fact]
    public void RefreshTokenExpiresIn_WhenAbsent_IsNull()
    {
        var response = JsonSerializer.Deserialize<TokenResponse>(Document);

        Assert.NotNull(response);
        Assert.Null(response.RefreshTokenExpiresIn);
    }

    #endregion
}
