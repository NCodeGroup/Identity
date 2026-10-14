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

using System.Buffers.Text;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

public class TokenEndpointPasswordTests
{
    private const string ClientId = "it-pwd-client";
    private const string ClientSecret = "confidential-secret-value";
    private const string UserName = "alice";
    private const string Password = "correct-horse-battery-staple";

    [Fact]
    public async Task PostToken_WithResourceOwnerPassword_ReturnsAccessToken()
    {
        using var factory = new PlaygroundApplicationFactory();
        await factory.SeedConfidentialClientAsync(
            ClientId,
            ClientSecret,
            OpenIdConstants.GrantTypes.Password
        );
        await factory.SeedResourceServerWithClientGrantAsync(
            ClientId,
            "it-rs-api",
            "https://api.integration.test",
            TestServerSettingsProvider.ApiScope
        );
        var localAccountId = await factory.SeedLocalAccountAsync(UserName, Password);

        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        async Task<(HttpStatusCode Status, string Body)> RequestTokenAsync()
        {
            using var content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = OpenIdConstants.GrantTypes.Password,
                    ["client_id"] = ClientId,
                    ["client_secret"] = ClientSecret,
                    ["username"] = UserName,
                    ["password"] = Password,
                    ["scope"] = TestServerSettingsProvider.ApiScope,
                }
            );
            using var response = await client.PostAsync("/oauth2/token", content);
            return (response.StatusCode, await response.Content.ReadAsStringAsync());
        }

        var (status, body) = await RequestTokenAsync();
        Assert.True(status == HttpStatusCode.OK, body);

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("access_token", out var accessToken));
        Assert.False(string.IsNullOrEmpty(accessToken.GetString()));
        Assert.Equal(OpenIdConstants.TokenTypes.Bearer, root.GetProperty("token_type").GetString());

        // The access token's subject is the stable, server-owned principal id resolved for the authenticated account —
        // never the raw local-account id or username (ADR-0035).
        using var payload = DecodeJwtPayload(accessToken.GetString()!);
        var subject = payload.RootElement.GetProperty("sub").GetString();
        Assert.False(string.IsNullOrEmpty(subject));
        Assert.NotEqual(localAccountId, subject);
        Assert.NotEqual(UserName, subject);

        // Re-authenticating the same account resolves to the same principal, so the subject is stable across grants.
        var (secondStatus, secondBody) = await RequestTokenAsync();
        Assert.True(secondStatus == HttpStatusCode.OK, secondBody);
        using var secondDocument = JsonDocument.Parse(secondBody);
        using var secondPayload = DecodeJwtPayload(
            secondDocument.RootElement.GetProperty("access_token").GetString()!
        );
        Assert.Equal(subject, secondPayload.RootElement.GetProperty("sub").GetString());
    }

    [Fact]
    public async Task PostToken_WithWrongPassword_DoesNotReturnAccessToken()
    {
        using var factory = new PlaygroundApplicationFactory();
        await factory.SeedConfidentialClientAsync(
            ClientId,
            ClientSecret,
            OpenIdConstants.GrantTypes.Password
        );
        await factory.SeedResourceServerWithClientGrantAsync(
            ClientId,
            "it-rs-api",
            "https://api.integration.test",
            TestServerSettingsProvider.ApiScope
        );
        await factory.SeedLocalAccountAsync(UserName, Password);

        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdConstants.GrantTypes.Password,
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
                ["username"] = UserName,
                ["password"] = "wrong-password",
                ["scope"] = TestServerSettingsProvider.ApiScope,
            }
        );

        using var response = await client.PostAsync("/oauth2/token", content);

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.TryGetProperty("error", out _));
    }

    private static JsonDocument DecodeJwtPayload(string jwt)
    {
        var payloadSegment = jwt.Split('.')[1];
        var payloadBytes = Base64Url.DecodeFromChars(payloadSegment);
        return JsonDocument.Parse(payloadBytes);
    }
}
