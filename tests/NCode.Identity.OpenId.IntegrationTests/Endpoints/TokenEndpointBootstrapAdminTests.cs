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
using Microsoft.Extensions.Configuration;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

public class TokenEndpointBootstrapAdminTests
{
    private const string BootstrapClientId = "it-bootstrap-admin";
    private const string BootstrapClientSecret = "bootstrap-secret-value";
    private const string ManagementAudience = "urn:ncode:management";
    private const string ManagementScope = "read:clients";

    [Fact]
    public async Task PostToken_WithConfiguredBootstrapAdmin_SelfSeedsAndIssuesGlobalAdminToken()
    {
        using var factory = new PlaygroundApplicationFactory();

        // The bootstrap credential is supplied out of band (ADR-0044); nothing is pre-seeded.
        using var configured = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration(
                (_, config) =>
                    config.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["BootstrapAdmin:ClientId"] = BootstrapClientId,
                            ["BootstrapAdmin:ClientSecret"] = BootstrapClientSecret,
                        }
                    )
            )
        );

        var client = configured.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        // The first token request provisions the tenant (seeding the bootstrap client) before client authentication,
        // so this single call self-bootstraps.
        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdConstants.GrantTypes.ClientCredentials,
                ["client_id"] = BootstrapClientId,
                ["client_secret"] = BootstrapClientSecret,
                ["scope"] = ManagementScope,
            }
        );

        using var response = await client.PostAsync("/oauth2/token", content);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.True(root.TryGetProperty("access_token", out var accessToken));
        Assert.False(string.IsNullOrEmpty(accessToken.GetString()));

        using var payload = DecodeJwtPayload(accessToken.GetString()!);
        var payloadRoot = payload.RootElement;

        // The bootstrap client's management token is stamped with the GlobalAdmin role at issuance (ADR-0044).
        Assert.Equal("GlobalAdmin", payloadRoot.GetProperty("role").GetString());

        var audElement = payloadRoot.GetProperty("aud");
        var audiences =
            audElement.ValueKind == JsonValueKind.Array
                ? audElement.EnumerateArray().Select(element => element.GetString()).ToList()
                : [audElement.GetString()];
        Assert.Contains(ManagementAudience, audiences);
    }

    [Fact]
    public async Task PostToken_WhenBootstrapNotConfigured_DoesNotIssueTokenForBootstrapClient()
    {
        using var factory = new PlaygroundApplicationFactory();

        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdConstants.GrantTypes.ClientCredentials,
                ["client_id"] = BootstrapClientId,
                ["client_secret"] = BootstrapClientSecret,
                ["scope"] = ManagementScope,
            }
        );

        using var response = await client.PostAsync("/oauth2/token", content);

        // Absent configuration, the bootstrap client is never seeded, so there is no client to authenticate.
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    private static JsonDocument DecodeJwtPayload(string jwt)
    {
        var payloadSegment = jwt.Split('.')[1];
        var payloadBytes = Base64Url.DecodeFromChars(payloadSegment);
        return JsonDocument.Parse(payloadBytes);
    }
}
