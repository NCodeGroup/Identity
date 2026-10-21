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
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Authentication.Usage;
using NCode.Identity.OpenId.Host;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

// A tenant's admitted requests reach the durable usage ledger through each replica's periodic flush and are read back
// through the management API (ADR-0199).
public class TenantUsageTests
{
    private const string BootstrapClientId = "it-bootstrap-admin";
    private const string BootstrapClientSecret = "bootstrap-secret-value";
    private const string UsagePath = $"/api/tenants/{HostApplicationFactory.TenantId}/usage";

    #region Helpers

    private static WebApplicationFactory<HostApiMarker> CreateFactory(
        ConfiguredHostApplicationFactory host
    ) =>
        host.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.Configure<TenantUsageOptions>(options =>
                    options.FlushInterval = TimeSpan.FromMilliseconds(50)
                )
            )
        );

    private static ConfiguredHostApplicationFactory CreateHost() =>
        new(
            new Dictionary<string, string?>
            {
                ["BootstrapAdmin:ClientId"] = BootstrapClientId,
                ["BootstrapAdmin:ClientSecret"] = Base64Url.EncodeToString(
                    Encoding.UTF8.GetBytes(BootstrapClientSecret)
                ),
                ["BootstrapAdmin:TenantId"] = HostApplicationFactory.TenantId,
            }
        );

    private static HttpClient CreateClient(WebApplicationFactory<HostApiMarker> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static async Task<HttpClient> CreateAdministratorClientAsync(
        WebApplicationFactory<HostApiMarker> factory
    )
    {
        var client = CreateClient(factory);
        var credentials = Encoding.UTF8.GetBytes($"{BootstrapClientId}:{BootstrapClientSecret}");
        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "/oauth2/token")
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
                    ["grant_type"] = OpenIdConstants.GrantTypes.ClientCredentials,
                    ["scope"] = "read:clients",
                }
            ),
        };
        using var tokenResponse = await client.SendAsync(tokenRequest);
        var tokenBody = await tokenResponse.Content.ReadAsStringAsync();
        Assert.True(tokenResponse.StatusCode == HttpStatusCode.OK, tokenBody);

        using var token = JsonDocument.Parse(tokenBody);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token.RootElement.GetProperty("access_token").GetString()
        );
        return client;
    }

    private static long GetCount(JsonElement usage, string meter) =>
        usage
            .GetProperty("entries")
            .EnumerateArray()
            .Where(entry => entry.GetProperty("meter").GetString() == meter)
            .Sum(entry => entry.GetProperty("count").GetInt64());

    #endregion

    #region Usage Tests

    [Fact]
    public async Task GetUsage_AfterAdmittedRequests_ReturnsTheFlushedCounts()
    {
        using var host = CreateHost();
        using var factory = CreateFactory(host);
        using var administrator = await CreateAdministratorClientAsync(factory);
        var client = CreateClient(factory);

        for (var i = 0; i < 3; ++i)
        {
            using var discovery = await client.GetAsync("/.well-known/openid-configuration");
            Assert.Equal(HttpStatusCode.OK, discovery.StatusCode);
        }

        // The ledger is written by the periodic flush, so poll until the counts arrive. The three discovery requests
        // and the administrator's token request are admitted requests; the token request is also a confidential
        // client's token request.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var deadline = DateTime.UtcNow.AddSeconds(30);
        JsonElement usage;
        while (true)
        {
            using var response = await administrator.GetAsync(UsagePath);
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == HttpStatusCode.OK, body);
            usage = JsonDocument.Parse(body).RootElement.Clone();
            if (GetCount(usage, TenantUsageMeters.Requests) >= 4 || DateTime.UtcNow > deadline)
                break;
            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        Assert.Equal(HostApplicationFactory.TenantId, usage.GetProperty("tenantId").GetString());
        Assert.InRange(
            DateOnly.Parse(usage.GetProperty("to").GetString()!, CultureInfo.InvariantCulture),
            today,
            today.AddDays(1)
        );
        Assert.True(GetCount(usage, TenantUsageMeters.Requests) >= 4, usage.ToString());
        Assert.True(GetCount(usage, TenantUsageMeters.TokenRequests) >= 1, usage.ToString());
    }

    [Fact]
    public async Task GetUsage_WhenTheRangeIsReversed_ReturnsValidationProblem()
    {
        using var host = CreateHost();
        using var factory = CreateFactory(host);
        using var administrator = await CreateAdministratorClientAsync(factory);

        using var response = await administrator.GetAsync(
            $"{UsagePath}?from=2026-01-02&to=2026-01-01"
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, body);
        Assert.Contains("\"from\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetUsage_WhenAnonymous_ReturnsUnauthorized()
    {
        using var host = CreateHost();
        using var factory = CreateFactory(host);
        var client = CreateClient(factory);

        using var response = await client.GetAsync(UsagePath);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion
}
