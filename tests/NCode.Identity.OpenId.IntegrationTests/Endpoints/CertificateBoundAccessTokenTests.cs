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
/// Verifies that a server's support for certificate-bound access tokens (RFC 8705 §3.3) is advertised in discovery
/// and never forces a client to bind: only a client that requires binding (RFC 8705 §3.4) has its tokens bound.
/// </summary>
public class CertificateBoundAccessTokenTests
{
    private const string TokenPath = "/oauth2/token";
    private const string DiscoveryPath = "/.well-known/openid-configuration";
    private const string ClientSecret = "confidential-secret-value";
    private const string DiscoveryMember = "tls_client_certificate_bound_access_tokens";

    private static ConfiguredHostApplicationFactory CreateSupportingFactory() =>
        new(
            new Dictionary<string, string?>
            {
                ["OpenId:Server:Settings:tls_client_certificate_bound_access_tokens"] = "true",
            }
        );

    private static HttpClient CreateClient(HostApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task PostToken_WhenTheServerSupportsBindingAndTheClientDoesNotRequireIt_IssuesABearerTokenWithoutACertificate()
    {
        const string clientId = "it-cbat-optional";

        using var factory = CreateSupportingFactory();
        await factory.SeedConfidentialClientAsync(
            clientId,
            ClientSecret,
            OpenIdProtocol.GrantTypes.ClientCredentials
        );
        await factory.SeedResourceServerWithClientGrantAsync(
            clientId,
            $"{clientId}-rs-api",
            $"https://api.{clientId}.integration.test",
            TestServerSettingsProvider.ApiScope
        );

        var client = CreateClient(factory);
        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdProtocol.GrantTypes.ClientCredentials,
                ["scope"] = TestServerSettingsProvider.ApiScope,
                ["client_id"] = clientId,
                ["client_secret"] = ClientSecret,
            }
        );
        using var response = await client.PostAsync(TokenPath, content);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        using var document = JsonDocument.Parse(body);
        Assert.Equal(
            OpenIdConstants.TokenTypes.Bearer,
            document.RootElement.GetProperty("token_type").GetString()
        );
    }

    [Fact]
    public async Task GetDiscovery_WhenTheServerSupportsBinding_AdvertisesIt()
    {
        using var factory = CreateSupportingFactory();

        var client = CreateClient(factory);
        using var response = await client.GetAsync(DiscoveryPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.GetProperty(DiscoveryMember).GetBoolean());
    }

    [Fact]
    public async Task GetDiscovery_ByDefault_DoesNotAdvertiseBinding()
    {
        using var factory = new HostApplicationFactory();

        var client = CreateClient(factory);
        using var response = await client.GetAsync(DiscoveryPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(document.RootElement.GetProperty(DiscoveryMember).GetBoolean());
    }
}
