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

using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

// A password is judged, hashed and verified in its NFC form (ADR-0238).
public class PasswordNormalizationTests
{
    private const string ClientId = "it-normalization-client";
    private const string ClientSecret = "normalization-client-secret";
    private const string UserName = "normalization-user";

    // The same characters in two compositions: U+00E9, and "e" followed by the combining acute accent U+0301.
    private const string ComposedPassword = "caf\u00E9-normalization-password";
    private const string DecomposedPassword = "cafe\u0301-normalization-password";

    #region Scaffolding

    private static HttpClient CreateClient(HostApplicationFactory factory) =>
        factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );

    private static async Task<(HttpStatusCode StatusCode, string Body)> RequestTokenAsync(
        HttpClient client,
        string password
    )
    {
        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdProtocol.GrantTypes.Password,
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
                ["username"] = UserName,
                ["password"] = password,
                ["scope"] = OpenIdProtocol.ScopeTypes.OpenId,
            }
        );

        using var response = await client.PostAsync("/oauth2/token", content);
        var body = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, body);
    }

    #endregion

    #region Verification Tests

    [Fact]
    public async Task SignIn_WhenTheComposedFormIsTyped_VerifiesAPasswordSetDecomposed()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, DecomposedPassword);
        var client = CreateClient(factory);

        var sessionCookie = await InteractiveLogin.SignInAsync(client, UserName, ComposedPassword);

        Assert.False(string.IsNullOrEmpty(sessionCookie));
    }

    [Fact]
    public async Task PasswordGrant_WhenTheDecomposedFormIsTyped_VerifiesAPasswordSetComposed()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedConfidentialClientAsync(
            ClientId,
            ClientSecret,
            OpenIdProtocol.GrantTypes.Password
        );
        await factory.SeedLocalAccountAsync(UserName, ComposedPassword);
        var client = CreateClient(factory);

        var (statusCode, body) = await RequestTokenAsync(client, DecomposedPassword);

        Assert.True(statusCode == HttpStatusCode.OK, body);
    }

    #endregion
}
