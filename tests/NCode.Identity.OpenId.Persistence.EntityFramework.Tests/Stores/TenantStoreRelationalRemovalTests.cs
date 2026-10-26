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

using System.Text.Json;
using IdGen;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

// Validates TenantStore.RemoveAsync against a relational provider that enforces foreign keys (ADR-0169). The EF
// InMemory provider skips foreign keys, so the delete order and the exact set of rows removed are proven here.
public sealed class TenantStoreRelationalRemovalTests : IDisposable
{
    private const string TenantOneId = "tenant-1";
    private const string TenantTwoId = "tenant-2";

    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;
    private readonly OpenIdDbContext _dbContext;
    private readonly TenantStore _store;
    private readonly ResourceServerStore _resourceServerStore;
    private readonly RoleAssignmentStore _roleAssignmentStore;

    public TenantStoreRelationalRemovalTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options => options.UseSqlite(_connection));
        _provider = serviceCollection.BuildServiceProvider();

        _dbContext = _provider.GetRequiredService<OpenIdDbContext>();
        _dbContext.Database.EnsureCreated();

        var idGenerator = _provider.GetRequiredService<IIdGenerator<long>>();
        _store = new TenantStore(Mock.Of<IStoreProvider>(), idGenerator, _dbContext);
        _resourceServerStore = new ResourceServerStore(
            Mock.Of<IStoreProvider>(),
            idGenerator,
            _dbContext
        );
        _roleAssignmentStore = new RoleAssignmentStore(
            Mock.Of<IStoreProvider>(),
            idGenerator,
            _dbContext
        );
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _provider.Dispose();
        _connection.Dispose();
    }

    #region Helpers

    private sealed record RowCounts(
        int Tenants,
        int TenantSecrets,
        int Secrets,
        int ResourceServers,
        int Scopes,
        int ResourceServerSecrets,
        int RoleAssignments
    );

    private static PersistedSecret CreateSecret(string secretId) =>
        new()
        {
            SecretId = secretId,
            ConcurrencyToken = string.Empty,
            Use = "sig",
            Algorithm = "RS256",
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
            SecretType = SecretTypes.Symmetric,
            SecretEncoding = NCode.Identity.Secrets.SecretEncodings.SymmetricRaw,
            KeySizeBits = 256,
            EncodedValue = "encoded-value",
        };

    private static PersistedTenant CreateTenant(
        string tenantId,
        params PersistedSecret[] secrets
    ) =>
        new()
        {
            TenantId = tenantId,
            ConcurrencyToken = string.Empty,
            Issuer = null,
            IsDisabled = false,
            DisplayName = tenantId,
            Settings = new PersistedTenantSettings
            {
                TenantId = tenantId,
                ConcurrencyToken = string.Empty,
                Value = JsonSerializer.SerializeToElement(new Dictionary<string, object>()),
            },
            Secrets = new PersistedTenantSecrets
            {
                TenantId = tenantId,
                ConcurrencyToken = string.Empty,
                Value = secrets,
            },
        };

    private async Task SeedTenantAsync(string tenantId)
    {
        await _store.AddAsync(
            CreateTenant(
                tenantId,
                CreateSecret($"{tenantId}-secret-1"),
                CreateSecret($"{tenantId}-secret-2")
            ),
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    private async Task SeedResourceServerAsync(string tenantId, bool isSystem)
    {
        var resourceServerId = $"{(isSystem ? "system" : "api")}-{tenantId}";

        var resourceServer = new PersistedResourceServer
        {
            TenantId = tenantId,
            ResourceServerId = resourceServerId,
            Identifier = isSystem
                ? $"urn:ncode:openid:{tenantId}"
                : $"https://api.example.com/{tenantId}",
            ConcurrencyToken = string.Empty,
            Name = resourceServerId,
            IsSystem = isSystem,
            IsDisabled = false,
            Settings = new PersistedResourceServerSettings
            {
                TenantId = tenantId,
                ResourceServerId = resourceServerId,
                Value = JsonSerializer.SerializeToElement(new Dictionary<string, object>()),
            },
            Scopes =
            [
                new PersistedScope
                {
                    Value = "openid",
                    Description = "Sign you in and issue an identity token.",
                    IsSystem = isSystem,
                },
                new PersistedScope
                {
                    Value = "profile",
                    Description = "Read your profile.",
                    IsSystem = isSystem,
                },
            ],
            Secrets = new PersistedResourceServerSecrets
            {
                TenantId = tenantId,
                ResourceServerId = resourceServerId,
                Value = [CreateSecret($"{resourceServerId}-secret")],
            },
        };

        await _resourceServerStore.AddAsync(resourceServer, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    private async Task SeedRoleAssignmentAsync(string tenantId)
    {
        await _roleAssignmentStore.AddAsync(
            new PersistedRoleAssignment
            {
                TenantId = tenantId,
                AssignmentId = $"assign-{tenantId}",
                PrincipalId = "subject-1",
                RoleName = "Owner",
                ResourceType = "tenant",
                ResourceId = tenantId,
                ConcurrencyToken = string.Empty,
            },
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    private async Task SeedTenantWithSystemRowsAsync(string tenantId)
    {
        await SeedTenantAsync(tenantId);
        await SeedResourceServerAsync(tenantId, isSystem: true);
        await SeedRoleAssignmentAsync(tenantId);
    }

    private async Task SeedEndUserDataAsync(string tenantId)
    {
        var idGenerator = _provider.GetRequiredService<IIdGenerator<long>>();
        var grantStore = new GrantStore(Mock.Of<IStoreProvider>(), idGenerator, _dbContext);
        var principalStore = new FederatedPrincipalStore(
            Mock.Of<IStoreProvider>(),
            idGenerator,
            _dbContext
        );
        var identityStore = new FederatedIdentityStore(
            Mock.Of<IStoreProvider>(),
            idGenerator,
            _dbContext
        );

        await grantStore.AddAsync(
            new PersistedGrant
            {
                GrantType = "session",
                HashedKey = $"{tenantId}-session",
                GrantId = $"{tenantId}-session",
                ConcurrencyToken = string.Empty,
                TenantId = tenantId,
                ClientId = null,
                SubjectId = "subject-1",
                SessionId = null,
                RefreshTokenLineageId = null,
                CreatedWhen = DateTimeOffset.UnixEpoch,
                ExpiresWhen = DateTimeOffset.UnixEpoch.AddHours(1),
                RevokedWhen = null,
                ConsumedWhen = null,
                ProtectedPayload = "protected-payload",
            },
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();

        await principalStore.AddAsync(
            new PersistedFederatedPrincipal
            {
                TenantId = tenantId,
                PrincipalId = $"{tenantId}-principal",
                ConcurrencyToken = string.Empty,
            },
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();

        await identityStore.AddAsync(
            new PersistedFederatedIdentity
            {
                TenantId = tenantId,
                FederatedIdentityId = $"{tenantId}-identity",
                PrincipalId = $"{tenantId}-principal",
                Issuer = "https://issuer.example",
                Subject = $"{tenantId}-subject",
                JoinKey = null,
                ConcurrencyToken = string.Empty,
            },
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();

        _dbContext.ChangeTracker.Clear();
    }

    private async Task<RowCounts> CountRowsAsync()
    {
        _dbContext.ChangeTracker.Clear();

        return new RowCounts(
            Tenants: await _dbContext.Tenants.CountAsync(),
            TenantSecrets: await _dbContext.TenantSecrets.IgnoreQueryFilters().CountAsync(),
            Secrets: await _dbContext.Secrets.CountAsync(),
            ResourceServers: await _dbContext.ResourceServers.IgnoreQueryFilters().CountAsync(),
            Scopes: await _dbContext.Scopes.IgnoreQueryFilters().CountAsync(),
            ResourceServerSecrets: await _dbContext
                .ResourceServerSecrets.IgnoreQueryFilters()
                .CountAsync(),
            RoleAssignments: await _dbContext.RoleAssignments.IgnoreQueryFilters().CountAsync()
        );
    }

    #endregion

    #region RemoveAsync Tests

    [Fact]
    public async Task RemoveAsync_OnARelationalProvider_RemovesOnlyTheTenantsOwnRows()
    {
        await SeedTenantWithSystemRowsAsync(TenantOneId);
        await SeedTenantWithSystemRowsAsync(TenantTwoId);
        _dbContext.ChangeTracker.Clear();

        Assert.Equal(new RowCounts(2, 4, 6, 2, 4, 2, 2), await CountRowsAsync());

        var removed = await _store.RemoveAsync(TenantOneId, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        Assert.True(removed);
        Assert.Equal(new RowCounts(1, 2, 3, 1, 2, 1, 1), await CountRowsAsync());

        var tenant = Assert.Single(await _dbContext.Tenants.ToListAsync());
        Assert.Equal(TenantTwoId, tenant.TenantId);

        Assert.All(
            await _dbContext.TenantSecrets.IgnoreQueryFilters().ToListAsync(),
            entity => Assert.Equal(TenantTwoId, entity.NormalizedTenantId)
        );
        Assert.All(
            await _dbContext.ResourceServers.IgnoreQueryFilters().ToListAsync(),
            entity => Assert.Equal(TenantTwoId, entity.NormalizedTenantId)
        );
        Assert.All(
            await _dbContext.Scopes.IgnoreQueryFilters().ToListAsync(),
            entity => Assert.Equal(TenantTwoId, entity.NormalizedTenantId)
        );
        Assert.All(
            await _dbContext.ResourceServerSecrets.IgnoreQueryFilters().ToListAsync(),
            entity => Assert.Equal(TenantTwoId, entity.NormalizedTenantId)
        );
        Assert.All(
            await _dbContext.RoleAssignments.IgnoreQueryFilters().ToListAsync(),
            entity => Assert.Equal(TenantTwoId, entity.NormalizedTenantId)
        );
        Assert.All(
            await _dbContext.Secrets.ToListAsync(),
            entity => Assert.Contains(TenantTwoId, entity.SecretId)
        );
    }

    [Fact]
    public async Task RemoveAsync_OnARelationalProvider_WhenANonSystemResourceServerExists_ThrowsInvalidOperation()
    {
        await SeedTenantWithSystemRowsAsync(TenantOneId);
        await SeedTenantWithSystemRowsAsync(TenantTwoId);
        await SeedResourceServerAsync(TenantTwoId, isSystem: false);

        var before = await CountRowsAsync();
        Assert.Equal(new RowCounts(2, 4, 7, 3, 6, 3, 2), before);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _store.RemoveAsync(TenantTwoId, CancellationToken.None)
        );

        Assert.Equal(before, await CountRowsAsync());
    }

    [Fact]
    public async Task RemoveAsync_OnARelationalProvider_RemovesTheEndUserDataOfTheTenant()
    {
        await SeedTenantWithSystemRowsAsync(TenantOneId);
        await SeedTenantWithSystemRowsAsync(TenantTwoId);
        await SeedEndUserDataAsync(TenantOneId);
        await SeedEndUserDataAsync(TenantTwoId);

        Assert.Equal(2, await _dbContext.Grants.IgnoreQueryFilters().CountAsync());
        Assert.Equal(2, await _dbContext.FederatedPrincipals.IgnoreQueryFilters().CountAsync());
        Assert.Equal(2, await _dbContext.FederatedIdentities.IgnoreQueryFilters().CountAsync());

        var removed = await _store.RemoveAsync(TenantOneId, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        Assert.True(removed);

        var grant = Assert.Single(await _dbContext.Grants.IgnoreQueryFilters().ToListAsync());
        Assert.Equal(TenantTwoId, grant.NormalizedTenantId);

        var principal = Assert.Single(
            await _dbContext.FederatedPrincipals.IgnoreQueryFilters().ToListAsync()
        );
        Assert.Equal(TenantTwoId, principal.NormalizedTenantId);

        var identity = Assert.Single(
            await _dbContext.FederatedIdentities.IgnoreQueryFilters().ToListAsync()
        );
        Assert.Equal(TenantTwoId, identity.NormalizedTenantId);
    }

    [Fact]
    public async Task RemoveAsync_OnARelationalProvider_RemovesTheRoleAssignmentsItsPrincipalsHoldInOtherTenants()
    {
        await SeedTenantWithSystemRowsAsync(TenantOneId);
        await SeedTenantWithSystemRowsAsync(TenantTwoId);
        await SeedEndUserDataAsync(TenantOneId);
        await _roleAssignmentStore.AddAsync(
            new PersistedRoleAssignment
            {
                TenantId = TenantTwoId,
                AssignmentId = "assign-cross",
                PrincipalId = $"{TenantOneId}-principal",
                RoleName = "Owner",
                ResourceType = "tenant",
                ResourceId = TenantTwoId,
                ConcurrencyToken = string.Empty,
            },
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var removed = await _store.RemoveAsync(TenantOneId, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        Assert.True(removed);
        var remaining = await _dbContext
            .RoleAssignments.IgnoreQueryFilters()
            .Select(entity => entity.AssignmentId)
            .OrderBy(assignmentId => assignmentId)
            .ToListAsync();
        Assert.Equal(["assign-tenant-2"], remaining);
    }

    #endregion
}
