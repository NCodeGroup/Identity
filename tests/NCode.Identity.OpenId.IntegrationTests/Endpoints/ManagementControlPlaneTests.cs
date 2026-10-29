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
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using NCode.Buffers;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

/// <summary>
/// Drives the management API under each dynamic tenant strategy with a token the root (control-plane) tenant issued:
/// the API authenticates the caller against the root tenant whatever tenant the request's host or path addresses, and
/// the route's <c>tenantId</c> only scopes the data (ADR-0191). The static strategy, whose own tenant is the
/// control-plane tenant, is driven by every other management integration test.
/// </summary>
public class ManagementControlPlaneTests
{
    private const string BootstrapClientId = "it-bootstrap-admin";
    private const string BootstrapClientSecret = "bootstrap-secret-value";
    private const string HostIssuerTemplate = "https://{tenantId}.auth.test";
    private const string PathIssuerTemplate = "http://localhost/{tenantId}";

    #region Helpers

    private static ConfiguredHostApplicationFactory CreateFactory(
        Dictionary<string, string?> strategySettings
    ) =>
        new(
            new Dictionary<string, string?>(strategySettings)
            {
                ["BootstrapAdmin:ClientId"] = BootstrapClientId,
                ["BootstrapAdmin:ClientSecret"] = Base64Url.EncodeToString(
                    SecureEncoding.UTF8.GetBytes(BootstrapClientSecret)
                ),
                ["BootstrapAdmin:TenantId"] = "root",
            }
        );

    private static ConfiguredHostApplicationFactory CreateHostFactory() =>
        CreateFactory(
            new Dictionary<string, string?>
            {
                ["OpenId:TenantResolution:StrategyCode"] = OpenIdConstants
                    .TenantStrategyCodes
                    .DynamicByHost,
                ["OpenId:TenantResolution:Root:Issuer"] = "http://localhost",
                ["OpenId:TenantResolution:DynamicByHost:IssuerTemplate"] = HostIssuerTemplate,
            }
        );

    private static ConfiguredHostApplicationFactory CreatePathFactory() =>
        CreateFactory(
            new Dictionary<string, string?>
            {
                ["OpenId:TenantResolution:StrategyCode"] = OpenIdConstants
                    .TenantStrategyCodes
                    .DynamicByPath,
                ["OpenId:TenantResolution:DynamicByPath:IssuerTemplate"] = PathIssuerTemplate,
            }
        );

    private static async Task<HttpClient> CreateAdministratorClientAsync(
        HostApplicationFactory factory,
        string tokenPath
    )
    {
        var client = factory.CreateClient();

        var credentials = SecureEncoding.UTF8.GetBytes(
            $"{BootstrapClientId}:{BootstrapClientSecret}"
        );
        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, tokenPath)
        {
            Headers =
            {
                Authorization = new AuthenticationHeaderValue(
                    "Basic",
                    Convert.ToBase64String(credentials)
                ),
            },
            Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = OpenIdProtocol.GrantTypes.ClientCredentials,
                    ["scope"] = "read:clients",
                }
            ),
        };
        using var tokenResponse = await client.SendAsync(tokenRequest);
        var tokenBody = await tokenResponse.Content.ReadAsStringAsync();
        Assert.True(tokenResponse.StatusCode == HttpStatusCode.OK, tokenBody);

        var token = JsonElement.Parse(tokenBody);
        var accessToken = token.GetProperty("access_token").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            accessToken
        );
        return client;
    }

    private static async Task<JsonElement> CreateTenantAsync(
        HttpClient client,
        string tenantId,
        string? host = null
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/tenants")
        {
            Content = JsonContent.Create(
                new
                {
                    tenantId,
                    displayName = tenantId,
                    isDisabled = false,
                    settings = new { },
                }
            ),
        };
        if (host is not null)
            request.Headers.Host = host;

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);

        var document = JsonElement.Parse(body);
        return document;
    }

    #endregion

    #region DynamicByHost Tests

    [Fact]
    public async Task CreateTenant_WhenReachedOnAHostThatIsNoTenants_AuthenticatesAgainstTheRootTenant()
    {
        using var factory = CreateHostFactory();
        using var client = await CreateAdministratorClientAsync(factory, "/oauth2/token");

        var created = await CreateTenantAsync(client, "acme", host: "manage.auth.test");

        Assert.Equal("https://acme.auth.test", created.GetProperty("issuer").GetString());
    }

    [Fact]
    public async Task ListClients_WhenReachedOnAnotherTenantsHost_AuthenticatesAgainstTheRootTenant()
    {
        using var factory = CreateHostFactory();
        using var client = await CreateAdministratorClientAsync(factory, "/oauth2/token");
        await CreateTenantAsync(client, "acme");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/tenants/acme/clients");
        request.Headers.Host = "acme.auth.test";
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
    }

    #endregion

    #region DynamicByPath Tests

    [Fact]
    public async Task CreateTenant_WhenTheStrategyIsByPath_AuthenticatesAgainstTheRootTenant()
    {
        using var factory = CreatePathFactory();
        using var client = await CreateAdministratorClientAsync(factory, "/root/oauth2/token");

        var created = await CreateTenantAsync(client, "acme");

        Assert.Equal("http://localhost/acme", created.GetProperty("issuer").GetString());
    }

    [Fact]
    public async Task ListClients_WhenTheStrategyIsByPath_AuthenticatesAgainstTheRootTenant()
    {
        using var factory = CreatePathFactory();
        using var client = await CreateAdministratorClientAsync(factory, "/root/oauth2/token");
        await CreateTenantAsync(client, "acme");

        using var response = await client.GetAsync("/api/tenants/acme/clients");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
    }

    #endregion
}
