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
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

/// <summary>
/// Verifies the FAPI 2.0 <c>security_profile</c> mechanism (F16 Phase 2): selecting <c>fapi2</c> on a client tightens
/// its effective settings through the merge lattice, so weak (<c>client_secret</c>) authentication is rejected while a
/// non-profiled client with the same credentials succeeds.
/// </summary>
public class TokenEndpointSecurityProfileTests
{
    private const string ClientSecret = "confidential-secret-value-0123456789";
    private const string TokenPath = "/oauth2/token";

    private static HttpClient CreateClient(HostApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static FormUrlEncodedContent ClientSecretPostBody(string clientId) =>
        new(
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdConstants.GrantTypes.ClientCredentials,
                ["scope"] = TestServerSettingsProvider.ApiScope,
                ["client_id"] = clientId,
                ["client_secret"] = ClientSecret,
            }
        );

    [Fact]
    public async Task PostToken_ClientSecretPost_WithoutProfile_ReturnsAccessToken()
    {
        const string clientId = "it-profile-baseline";

        using var factory = new HostApplicationFactory();
        await factory.SeedConfidentialClientWithSecurityProfileAsync(
            clientId,
            ClientSecret,
            securityProfile: string.Empty,
            OpenIdConstants.GrantTypes.ClientCredentials
        );
        await factory.SeedResourceServerWithClientGrantAsync(
            clientId,
            "it-profile-base-rs-api",
            "https://api.profile-base.integration.test",
            TestServerSettingsProvider.ApiScope
        );

        var client = CreateClient(factory);
        using var response = await client.PostAsync(TokenPath, ClientSecretPostBody(clientId));

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
    }

    [Fact]
    public async Task PostToken_ClientSecretPost_UnderFapi2_Returns400()
    {
        const string clientId = "it-profile-fapi2";

        using var factory = new HostApplicationFactory();
        await factory.SeedConfidentialClientWithSecurityProfileAsync(
            clientId,
            ClientSecret,
            OpenIdConstants.SecurityProfiles.Fapi2,
            OpenIdConstants.GrantTypes.ClientCredentials
        );
        await factory.SeedResourceServerWithClientGrantAsync(
            clientId,
            "it-profile-fapi2-rs-api",
            "https://api.profile-fapi2.integration.test",
            TestServerSettingsProvider.ApiScope
        );

        var client = CreateClient(factory);
        using var response = await client.PostAsync(TokenPath, ClientSecretPostBody(clientId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            OpenIdConstants.ErrorCodes.InvalidClient,
            document.RootElement.GetProperty("error").GetString()
        );
    }

    [Fact]
    public async Task PostToken_WhenStoredProfileIsNotRegistered_IsRefusedRatherThanServedWithoutIt()
    {
        const string clientId = "it-profile-unknown";

        using var factory = new HostApplicationFactory();
        // The store is written directly, past every entry point that would reject the misspelled profile name.
        await factory.SeedConfidentialClientWithSecurityProfileAsync(
            clientId,
            ClientSecret,
            securityProfile: "fapi-2",
            OpenIdConstants.GrantTypes.ClientCredentials
        );
        await factory.SeedResourceServerWithClientGrantAsync(
            clientId,
            "it-profile-unknown-rs-api",
            "https://api.profile-unknown.integration.test",
            TestServerSettingsProvider.ApiScope
        );

        var client = CreateClient(factory);
        using var response = await client.PostAsync(TokenPath, ClientSecretPostBody(clientId));

        var body = await response.Content.ReadAsStringAsync();
        Assert.False(response.IsSuccessStatusCode, body);
        Assert.DoesNotContain("access_token", body, StringComparison.Ordinal);
    }
}
