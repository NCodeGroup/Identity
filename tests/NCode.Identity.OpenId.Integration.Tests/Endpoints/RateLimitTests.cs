// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Primitives;
using NCode.Collections.Providers;
using NCode.Identity.OpenId.Authentication.RateLimiting;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

/// <summary>
/// Drives a tenant past its <c>rate_limit_tenant_per_minute</c> limit and its daily quotas through the real host and
/// confirms the refused request is answered <c>429 Too Many Requests</c> with <c>Retry-After</c>.
/// </summary>
// See ADR-1196 and ADR-1198.
public class RateLimitTests
{
    private const string ClientId = "it-quota-client";
    private const string ClientSecret = "confidential-secret-value";
    private const string TokenPath = "/oauth2/token";
    private const string DiscoveryPath = "/.well-known/openid-configuration";
    private const string TestPartitionSettingName = "test_partition_per_minute";
    private const string TestPartitionHeaderName = "X-Test-Partition";

    private static ConfiguredHostApplicationFactory CreateFactory(
        string settingName,
        string value
    ) =>
        new(
            new Dictionary<string, string?> { [$"OpenId:Platform:Settings:{settingName}"] = value }
        );

    private static HttpClient CreateClient(HostApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static FormUrlEncodedContent ConfidentialBody() =>
        new(
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdProtocol.GrantTypes.ClientCredentials,
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
                ["scope"] = TestPlatformSettingsProvider.ApiScope,
            }
        );

    private static FormUrlEncodedContent AnonymousBody() =>
        new(
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdProtocol.GrantTypes.ClientCredentials,
            }
        );

    private static async ValueTask<HttpResponseMessage> GetDiscoveryAsync(
        HttpClient client,
        string? partition,
        CancellationToken cancellationToken
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, DiscoveryPath);
        if (partition is not null)
            request.Headers.Add(TestPartitionHeaderName, partition);

        return await client.SendAsync(request, cancellationToken);
    }

    private static async ValueTask AssertTooManyRequestsAsync(
        HttpResponseMessage response,
        TimeSpan maxRetryAfter,
        CancellationToken cancellationToken
    )
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.TooManyRequests, body);
        var retryAfter = response.Headers.RetryAfter?.Delta;
        Assert.NotNull(retryAfter);
        Assert.InRange(retryAfter.Value, TimeSpan.FromSeconds(1), maxRetryAfter);
        Assert.True(response.Headers.CacheControl?.NoStore);

        var jsonElement = JsonElement.Parse(body);
        Assert.Equal(
            OpenIdProtocol.ErrorCodes.TemporarilyUnavailable,
            jsonElement.GetProperty("error").GetString()
        );
    }

    #region Rate Limit Tests

    [Fact]
    public async Task Discovery_WhenTheTenantIsOverItsRateLimit_ReturnsTooManyRequests()
    {
        // The platform's settings cascade into the tenant, so a platform-wide limit of two applies to the static tenant.
        using var factory = new ConfiguredHostApplicationFactory(
            new Dictionary<string, string?>
            {
                [$"OpenId:Platform:Settings:{OpenIdSettingNames.RateLimitTenantPerMinute}"] = "2",
            }
        );
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        // The third request is over the limit whether or not the minute rolls over between the requests: the previous
        // window's count weighs almost fully at the start of the next one.
        for (var i = 0; i < 2; ++i)
        {
            using var admitted = await client.GetAsync("/.well-known/openid-configuration");
            Assert.Equal(HttpStatusCode.OK, admitted.StatusCode);
        }

        using var response = await client.GetAsync("/.well-known/openid-configuration");

        await AssertTooManyRequestsAsync(
            response,
            TimeSpan.FromSeconds(120),
            CancellationToken.None
        );
    }

    #endregion

    #region Quota Tests

    [Fact]
    public async Task PostToken_WhenTheTenantHasSpentItsDailyQuota_ReturnsTooManyRequestsUntilMidnight()
    {
        using var factory = CreateFactory(OpenIdSettingNames.QuotaTenantRequestsPerDay, "2");
        var client = CreateClient(factory);

        // A quota is one fixed window per UTC day, so the third request is refused unless midnight UTC passes between
        // the requests; Retry-After then points at most one day ahead. A request the endpoint itself rejects still
        // spends the quota.
        for (var i = 0; i < 2; ++i)
        {
            using var admitted = await client.PostAsync(TokenPath, AnonymousBody());
            Assert.NotEqual(HttpStatusCode.TooManyRequests, admitted.StatusCode);
        }

        using var response = await client.PostAsync(TokenPath, AnonymousBody());

        await AssertTooManyRequestsAsync(response, TimeSpan.FromDays(1), CancellationToken.None);
    }

    [Fact]
    public async Task Discovery_WhenRequestedRepeatedly_DoesNotSpendTheDailyQuota()
    {
        // Discovery and the JSON Web Key Set are public metadata, so anonymous callers cannot spend the tenant's quota
        // and refuse its clients (ADR-1198); the per-minute limits still cover them.
        using var factory = CreateFactory(OpenIdSettingNames.QuotaTenantRequestsPerDay, "1");
        var client = CreateClient(factory);

        for (var i = 0; i < 3; ++i)
        {
            using var discovery = await client.GetAsync("/.well-known/openid-configuration");
            Assert.Equal(HttpStatusCode.OK, discovery.StatusCode);
            using var jwks = await client.GetAsync("/oauth2/jwks");
            Assert.Equal(HttpStatusCode.OK, jwks.StatusCode);
        }

        using var response = await client.PostAsync(TokenPath, AnonymousBody());

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode != HttpStatusCode.TooManyRequests, body);
    }

    [Fact]
    public async Task PostToken_WhenAConfidentialClientHasSpentTheTokenQuota_ReturnsTooManyRequests()
    {
        using var factory = CreateFactory(OpenIdSettingNames.QuotaTenantTokenRequestsPerDay, "1");
        await factory.SeedConfidentialClientAsync(
            ClientId,
            ClientSecret,
            [],
            CancellationToken.None
        );
        var client = CreateClient(factory);

        using var first = await client.PostAsync(TokenPath, ConfidentialBody());
        using var second = await client.PostAsync(TokenPath, ConfidentialBody());

        Assert.NotEqual(HttpStatusCode.TooManyRequests, first.StatusCode);
        await AssertTooManyRequestsAsync(second, TimeSpan.FromDays(1), CancellationToken.None);
    }

    [Fact]
    public async Task PostToken_WhenAPublicClientRequestsTokens_DoesNotSpendTheTokenQuota()
    {
        // A public client is named by its client_id alone, so its requests must not spend the tenant's token quota.
        using var factory = CreateFactory(OpenIdSettingNames.QuotaTenantTokenRequestsPerDay, "1");
        await factory.SeedPublicClientAsync(
            ClientId,
            ["https://client.example/callback"],
            CancellationToken.None
        );
        var client = CreateClient(factory);

        for (var i = 0; i < 3; ++i)
        {
            using var content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = OpenIdProtocol.GrantTypes.ClientCredentials,
                    ["client_id"] = ClientId,
                }
            );
            using var response = await client.PostAsync(TokenPath, content);

            var body = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode != HttpStatusCode.TooManyRequests, body);
        }
    }

    #endregion

    #region Registered Limit Tests

    [Fact]
    public async Task Discovery_WhenARegisteredLimitIsExceeded_RefusesOnlyItsPartition()
    {
        // The limit is registered beside the built-in ones, and a request with another partition value, or with none,
        // does not count against the partition that is over its limit.
        using var factory = new RegisteredLimitApplicationFactory();
        var client = CreateClient(factory);

        using var first = await GetDiscoveryAsync(client, "a", CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var second = await GetDiscoveryAsync(client, "a", CancellationToken.None);
        await AssertTooManyRequestsAsync(second, TimeSpan.FromSeconds(120), CancellationToken.None);

        using var otherPartition = await GetDiscoveryAsync(client, "b", CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, otherPartition.StatusCode);

        using var noPartition = await GetDiscoveryAsync(client, null, CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, noPartition.StatusCode);
    }

    #endregion

    // A host with a rate limit of one request a minute per value of the X-Test-Partition request header, registered
    // the way a package contributes its own limit: a setting descriptor and a rate-limit descriptor.
    private sealed class RegisteredLimitApplicationFactory : HostApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureAppConfiguration(configurationBuilder =>
                configurationBuilder.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        [$"OpenId:Platform:Settings:{TestPartitionSettingName}"] = "1",
                    }
                )
            );

            builder.ConfigureTestServices(serviceCollection =>
            {
                serviceCollection.TryAddEnumerable(
                    ServiceDescriptor.Singleton<
                        ICollectionDataSource<SettingDescriptor>,
                        TestPartitionSettingDescriptorDataSource
                    >()
                );
                serviceCollection.TryAddEnumerable(
                    ServiceDescriptor.Singleton<
                        ICollectionDataSource<RateLimitDescriptor>,
                        TestPartitionRateLimitDescriptorDataSource
                    >()
                );
            });
        }
    }

    private sealed class TestPartitionSettingDescriptorDataSource
        : ICollectionDataSource<SettingDescriptor>
    {
        public IChangeToken GetChangeToken() => NullChangeToken.Singleton;

        public IEnumerable<SettingDescriptor> Collection
        {
            get
            {
                yield return new SettingDescriptor<int>
                {
                    Name = TestPartitionSettingName,
                    AppliesTo = [OpenIdSettingScopes.Tenant],
                    Rule = SettingRules.AtLeast(1),
                    OnMerge = SettingMerge.Min,
                };
            }
        }
    }

    private sealed class TestPartitionRateLimitDescriptorDataSource
        : ICollectionDataSource<RateLimitDescriptor>
    {
        public IChangeToken GetChangeToken() => NullChangeToken.Singleton;

        public IEnumerable<RateLimitDescriptor> Collection
        {
            get
            {
                yield return new RateLimitDescriptor
                {
                    Name = "test_partition",
                    Stage = RateLimitStages.Request,
                    Order = 150,
                    Window = TimeSpan.FromMinutes(1),
                    IsSliding = true,
                    LimitKey = new SettingKey<int>(TestPartitionSettingName),
                    PartitionValueSelector = static context =>
                    {
                        var headers = context.OpenIdContext.Http.Request.Headers;
                        var value = headers[TestPartitionHeaderName].ToString();
                        return string.IsNullOrEmpty(value) ? null : value;
                    },
                };
            }
        }
    }
}
