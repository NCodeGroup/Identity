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

using System.Diagnostics;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

// Grants that ended before their retention are removed by the host's hosted service (ADR-0218).
public class GrantPruneTests
{
    private const string GrantType = "authorization_code";

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan PollTimeout = TimeSpan.FromSeconds(15);

    #region Helpers

    private static PersistedGrant CreateGrant(
        string grantId,
        DateTimeOffset createdWhen,
        DateTimeOffset? expiresWhen,
        DateTimeOffset? revokedWhen
    ) =>
        new()
        {
            GrantType = GrantType,
            HashedKey = $"hashed-key-{grantId}",
            GrantId = grantId,
            ConcurrencyToken = string.Empty,
            TenantId = HostApplicationFactory.TenantId,
            ClientId = null,
            SubjectId = "subject-1",
            SessionId = null,
            RefreshTokenLineageId = null,
            CreatedWhen = createdWhen,
            ExpiresWhen = expiresWhen,
            RevokedWhen = revokedWhen,
            ConsumedWhen = null,
            ProtectedPayload = "protected-payload",
        };

    private static async Task AddAsync(IServiceProvider services, params PersistedGrant[] grants)
    {
        using var scope = services.CreateScope();
        var storeManagerFactory = scope.ServiceProvider.GetRequiredService<IStoreManagerFactory>();
        await using var storeManager = await storeManagerFactory.CreateAsync(
            CancellationToken.None
        );
        var store = storeManager.GetStore<IGrantStore>();
        foreach (var grant in grants)
        {
            await store.AddAsync(grant, CancellationToken.None);
        }

        await storeManager.SaveChangesAsync(CancellationToken.None);
    }

    private static async Task<PersistedGrant?> FindAsync(IServiceProvider services, string grantId)
    {
        using var scope = services.CreateScope();
        var storeManagerFactory = scope.ServiceProvider.GetRequiredService<IStoreManagerFactory>();
        await using var storeManager = await storeManagerFactory.CreateAsync(
            CancellationToken.None
        );
        var store = storeManager.GetStore<IGrantStore>();
        return await store.GetOrDefaultAsync(grantId, CancellationToken.None);
    }

    #endregion

    #region Prune Tests

    [Fact]
    public async Task HostedService_RemovesTheGrantsThatEndedBeforeTheRetention()
    {
        using var factory = new HostApplicationFactory();
        using var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.Configure<GrantRetentionOptions>(options =>
                {
                    options.PruneInterval = TimeSpan.FromMilliseconds(50);
                    options.Retention = TimeSpan.FromDays(1);
                })
            )
        );

        // A first request makes the host persist the tenant, which a grant requires as a foreign key.
        using (var warmupClient = host.CreateClient())
        {
            using var _ = await warmupClient.GetAsync("/oauth2/jwks");
        }

        var now = DateTimeOffset.UtcNow;
        await AddAsync(
            host.Services,
            CreateGrant("expired", now.AddDays(-3), now.AddDays(-2), revokedWhen: null),
            CreateGrant("revoked", now.AddDays(-3), now.AddDays(10), revokedWhen: now.AddDays(-2)),
            CreateGrant("live", now.AddDays(-3), now.AddDays(1), revokedWhen: null)
        );

        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < PollTimeout)
        {
            if (
                await FindAsync(host.Services, "expired") is null
                && await FindAsync(host.Services, "revoked") is null
            )
            {
                break;
            }

            await Task.Delay(PollInterval);
        }

        Assert.Null(await FindAsync(host.Services, "expired"));
        Assert.Null(await FindAsync(host.Services, "revoked"));
        Assert.NotNull(await FindAsync(host.Services, "live"));
    }

    #endregion
}
