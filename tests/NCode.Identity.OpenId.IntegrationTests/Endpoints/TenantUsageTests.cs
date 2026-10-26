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
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NCode.Buffers;
using NCode.Identity.OpenId.Authentication.Usage;
using NCode.Identity.OpenId.Host;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Persistence.Stores;
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

    private static ConfiguredHostApplicationFactory CreateHost(
        IReadOnlyDictionary<string, string?>? settings = null
    )
    {
        var configuration = new Dictionary<string, string?>
        {
            ["BootstrapAdmin:ClientId"] = BootstrapClientId,
            ["BootstrapAdmin:ClientSecret"] = Base64Url.EncodeToString(
                SecureEncoding.UTF8.GetBytes(BootstrapClientSecret)
            ),
            ["BootstrapAdmin:TenantId"] = HostApplicationFactory.TenantId,
        };
        foreach (var (name, value) in settings ?? new Dictionary<string, string?>())
            configuration[$"OpenId:Server:Settings:{name}"] = value;

        return new ConfiguredHostApplicationFactory(configuration);
    }

    // The ledger is written by the periodic flush, so poll until the meter reaches the expected count.
    private static async Task<JsonElement> WaitForUsageAsync(
        HttpClient administrator,
        string meter,
        long expected
    )
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            using var response = await administrator.GetAsync(UsagePath);
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == HttpStatusCode.OK, body);
            var usage = JsonDocument.Parse(body).RootElement.Clone();
            if (GetCount(usage, meter) >= expected || DateTime.UtcNow > deadline)
                return usage;
            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }
    }

    private static HttpClient CreateClient(WebApplicationFactory<HostApiMarker> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static async Task<HttpClient> CreateAdministratorClientAsync(
        WebApplicationFactory<HostApiMarker> factory
    )
    {
        var client = CreateClient(factory);
        var credentials = SecureEncoding.UTF8.GetBytes(
            $"{BootstrapClientId}:{BootstrapClientSecret}"
        );
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

    // An anonymous token request is metered: the endpoint rejects it, but no rate limit or quota refuses it.
    private static async Task<HttpStatusCode> PostAnonymousTokenRequestAsync(HttpClient client)
    {
        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdConstants.GrantTypes.ClientCredentials,
            }
        );
        using var response = await client.PostAsync("/oauth2/token", content);
        return response.StatusCode;
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
            Assert.NotEqual(
                HttpStatusCode.TooManyRequests,
                await PostAnonymousTokenRequestAsync(client)
            );
        }

        // The ledger is written by the periodic flush, so poll until the counts arrive. The three anonymous token
        // requests and the administrator's token request are admitted requests; the administrator's is also a
        // confidential client's token request.
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
    public async Task GetUsage_WhenTheTierAdmitsOverage_ReportsTheOverageUntilItsCeiling()
    {
        using var host = CreateHost(
            new Dictionary<string, string?>
            {
                [OpenIdSettingNames.QuotaTenantRequestsPerDay] = "2",
                [OpenIdSettingNames.QuotaTenantRequestsOveragePerDay] = "2",
            }
        );
        using var factory = CreateFactory(host);
        using var administrator = await CreateAdministratorClientAsync(factory);
        var client = CreateClient(factory);

        // The administrator's token request spent the first of the two requests the quota allows; the first anonymous
        // token request spends the second, the next two are admitted as overage, and the overage ceiling refuses the
        // last. Discovery is public metadata, so it spends neither (ADR-0198).
        using (var discovery = await client.GetAsync("/.well-known/openid-configuration"))
            Assert.Equal(HttpStatusCode.OK, discovery.StatusCode);

        var statusCodes = new List<HttpStatusCode>();
        for (var i = 0; i < 4; ++i)
            statusCodes.Add(await PostAnonymousTokenRequestAsync(client));

        Assert.DoesNotContain(HttpStatusCode.TooManyRequests, statusCodes[..3]);
        Assert.Equal(HttpStatusCode.TooManyRequests, statusCodes[3]);

        var usage = await WaitForUsageAsync(administrator, TenantUsageMeters.Requests, 4);
        usage = await WaitForUsageAsync(administrator, TenantUsageMeters.RequestsOverage, 2);
        Assert.Equal(4, GetCount(usage, TenantUsageMeters.Requests));
        Assert.Equal(2, GetCount(usage, TenantUsageMeters.RequestsOverage));
    }

    [Fact]
    public async Task GetUsage_WhenTheClientLimitRefusesARequest_DoesNotRecordIt()
    {
        // The per-client limit refuses inside the token endpoint, after the tenant-wide limits admitted the request; a
        // refused request is not usage (ADR-0199).
        using var host = CreateHost(
            new Dictionary<string, string?> { [OpenIdSettingNames.RateLimitClientPerMinute] = "1" }
        );
        using var factory = CreateFactory(host);
        using var administrator = await CreateAdministratorClientAsync(factory);
        var client = CreateClient(factory);

        var credentials = SecureEncoding.UTF8.GetBytes(
            $"{BootstrapClientId}:{BootstrapClientSecret}"
        );
        using var refusedRequest = new HttpRequestMessage(HttpMethod.Post, "/oauth2/token")
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
        using (var refused = await client.SendAsync(refusedRequest))
            Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.NotEqual(
            HttpStatusCode.TooManyRequests,
            await PostAnonymousTokenRequestAsync(client)
        );

        // The administrator's token request and the anonymous one are usage; the refused request is not. Wait for both,
        // then for a few more flushes, so a recorded refusal would have reached the ledger too.
        await WaitForUsageAsync(administrator, TenantUsageMeters.Requests, 2);
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        var usage = await WaitForUsageAsync(administrator, TenantUsageMeters.Requests, 2);
        Assert.Equal(2, GetCount(usage, TenantUsageMeters.Requests));
        Assert.Equal(1, GetCount(usage, TenantUsageMeters.TokenRequests));
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

    #region Shutdown Tests

    [Fact]
    public async Task HostStop_AfterAdmittedRequests_AddsTheirUsageToTheLedger()
    {
        // The default one-minute flush interval means only the final flush at the host's stop writes the ledger. The
        // factory shares its in-memory database with the host it derives, so the parent factory reads what the stopped
        // host added.
        using var host = CreateHost();
        var recordingHost = host.WithWebHostBuilder(_ => { });
        var client = CreateClient(recordingHost);
        for (var i = 0; i < 3; ++i)
        {
            Assert.NotEqual(
                HttpStatusCode.TooManyRequests,
                await PostAnonymousTokenRequestAsync(client)
            );
        }

        await recordingHost.DisposeAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var storeManagerFactory = host.Services.GetRequiredService<IStoreManagerFactory>();
        await using var storeManager = await storeManagerFactory.CreateAsync(
            CancellationToken.None
        );
        var usage = await storeManager
            .GetStore<ITenantUsageStore>()
            .ListAsync(
                HostApplicationFactory.TenantId,
                today.AddDays(-1),
                today.AddDays(1),
                CancellationToken.None
            );
        Assert.Equal(
            3L,
            usage
                .Where(entry => entry.Meter == TenantUsageMeters.Requests)
                .Sum(entry => entry.Count)
        );
    }

    #endregion
}
