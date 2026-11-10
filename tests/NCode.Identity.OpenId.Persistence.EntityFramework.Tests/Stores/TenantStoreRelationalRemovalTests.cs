// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using IdGen;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

// Validates TenantStore.RemoveAsync against a relational provider that enforces foreign keys (ADR-1169). The EF
// InMemory provider skips foreign keys, so the delete order and the exact set of rows removed are proven here.
public sealed class TenantStoreRelationalRemovalTests : IDisposable
{
    private const string TenantOneId = "tenant-1";
    private const string TenantTwoId = "tenant-2";

    private SqliteConnection Connection { get; }
    private ServiceProvider Provider { get; }
    private OpenIdDbContext DbContext { get; }
    private TenantStore Store { get; }
    private ResourceServerStore ResourceServerStore { get; }
    private RoleAssignmentStore RoleAssignmentStore { get; }

    public TenantStoreRelationalRemovalTests()
    {
        Connection = new SqliteConnection("DataSource=:memory:");
        Connection.Open();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options => options.UseSqlite(Connection));
        Provider = serviceCollection.BuildServiceProvider();

        DbContext = Provider.GetRequiredService<OpenIdDbContext>();
        DbContext.Database.EnsureCreated();
        DbContext.SeedTeam(Provider.GetRequiredService<IIdGenerator<long>>(), "platform");

        var idGenerator = Provider.GetRequiredService<IIdGenerator<long>>();
        Store = new TenantStore(Mock.Of<IStoreProvider>(), idGenerator, DbContext);
        ResourceServerStore = new ResourceServerStore(
            Mock.Of<IStoreProvider>(),
            idGenerator,
            DbContext
        );
        RoleAssignmentStore = new RoleAssignmentStore(
            Mock.Of<IStoreProvider>(),
            idGenerator,
            DbContext
        );
    }

    public void Dispose()
    {
        DbContext.Dispose();
        Provider.Dispose();
        Connection.Dispose();
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
        new PersistedSecretBuilder
        {
            SecretId = secretId,
            Use = "sig",
            Algorithm = "RS256",
            EncodedValue = "encoded-value",
        }.Build();

    private static PersistedTenant CreateTenant(
        string tenantId,
        params PersistedSecret[] secrets
    ) => new PersistedTenantBuilder { TenantId = tenantId, Secrets = secrets }.Build();

    private async ValueTask SeedTenantAsync(string tenantId, CancellationToken cancellationToken)
    {
        await Store.AddAsync(
            CreateTenant(
                tenantId,
                CreateSecret($"{tenantId}-secret-1"),
                CreateSecret($"{tenantId}-secret-2")
            ),
            cancellationToken
        );
        await DbContext.SaveChangesAsync(cancellationToken);
        DbContext.ChangeTracker.Clear();
    }

    private async ValueTask SeedResourceServerAsync(
        string tenantId,
        bool isSystem,
        CancellationToken cancellationToken
    )
    {
        var resourceServerId = $"{(isSystem ? "system" : "api")}-{tenantId}";

        var resourceServer = new PersistedResourceServerBuilder
        {
            TenantId = tenantId,
            ResourceServerId = resourceServerId,
            Identifier = isSystem
                ? $"urn:ncode:openid:{tenantId}"
                : $"https://api.example.com/{tenantId}",
            Name = resourceServerId,
            IsSystem = isSystem,
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
            Secrets = [CreateSecret($"{resourceServerId}-secret")],
        }.Build();

        await ResourceServerStore.AddAsync(resourceServer, cancellationToken);
        await DbContext.SaveChangesAsync(cancellationToken);
        DbContext.ChangeTracker.Clear();
    }

    private async ValueTask SeedRoleAssignmentAsync(
        string tenantId,
        CancellationToken cancellationToken
    )
    {
        await RoleAssignmentStore.AddAsync(
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
            cancellationToken
        );
        await DbContext.SaveChangesAsync(cancellationToken);
        DbContext.ChangeTracker.Clear();
    }

    private async ValueTask SeedTenantWithSystemRowsAsync(
        string tenantId,
        CancellationToken cancellationToken
    )
    {
        await SeedTenantAsync(tenantId, cancellationToken);
        await SeedResourceServerAsync(tenantId, isSystem: true, cancellationToken);
        await SeedRoleAssignmentAsync(tenantId, cancellationToken);
    }

    private async ValueTask SeedEndUserDataAsync(
        string tenantId,
        CancellationToken cancellationToken
    )
    {
        var idGenerator = Provider.GetRequiredService<IIdGenerator<long>>();
        var grantStore = new GrantStore(Mock.Of<IStoreProvider>(), idGenerator, DbContext);
        var principalStore = new FederatedPrincipalStore(
            Mock.Of<IStoreProvider>(),
            idGenerator,
            DbContext
        );
        var identityStore = new FederatedIdentityStore(
            Mock.Of<IStoreProvider>(),
            idGenerator,
            DbContext
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
            cancellationToken
        );
        await DbContext.SaveChangesAsync(cancellationToken);

        await principalStore.AddAsync(
            new PersistedFederatedPrincipal
            {
                TenantId = tenantId,
                PrincipalId = $"{tenantId}-principal",
                ConcurrencyToken = string.Empty,
            },
            cancellationToken
        );
        await DbContext.SaveChangesAsync(cancellationToken);

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
            cancellationToken
        );
        await DbContext.SaveChangesAsync(cancellationToken);

        DbContext.ChangeTracker.Clear();
    }

    private async ValueTask<RowCounts> CountRowsAsync(CancellationToken cancellationToken)
    {
        DbContext.ChangeTracker.Clear();

        return new RowCounts(
            Tenants: await DbContext.Tenants.CountAsync(cancellationToken),
            TenantSecrets: await DbContext
                .TenantSecrets.IgnoreQueryFilters()
                .CountAsync(cancellationToken),
            Secrets: await DbContext.Secrets.CountAsync(cancellationToken),
            ResourceServers: await DbContext
                .ResourceServers.IgnoreQueryFilters()
                .CountAsync(cancellationToken),
            Scopes: await DbContext.Scopes.IgnoreQueryFilters().CountAsync(cancellationToken),
            ResourceServerSecrets: await DbContext
                .ResourceServerSecrets.IgnoreQueryFilters()
                .CountAsync(cancellationToken),
            RoleAssignments: await DbContext
                .RoleAssignments.IgnoreQueryFilters()
                .CountAsync(cancellationToken)
        );
    }

    #endregion

    #region RemoveAsync Tests

    [Fact]
    public async Task RemoveAsync_OnARelationalProvider_RemovesOnlyTheTenantsOwnRows()
    {
        await SeedTenantWithSystemRowsAsync(TenantOneId, CancellationToken.None);
        await SeedTenantWithSystemRowsAsync(TenantTwoId, CancellationToken.None);
        DbContext.ChangeTracker.Clear();

        Assert.Equal(
            new RowCounts(2, 4, 6, 2, 4, 2, 2),
            await CountRowsAsync(CancellationToken.None)
        );

        var removed = await Store.RemoveAsync(TenantOneId, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        Assert.True(removed);
        Assert.Equal(
            new RowCounts(1, 2, 3, 1, 2, 1, 1),
            await CountRowsAsync(CancellationToken.None)
        );

        var tenant = Assert.Single(await DbContext.Tenants.ToListAsync());
        Assert.Equal(TenantTwoId, tenant.TenantId);

        Assert.All(
            await DbContext.TenantSecrets.IgnoreQueryFilters().ToListAsync(),
            entity => Assert.Equal(TenantTwoId, entity.NormalizedTenantId)
        );
        Assert.All(
            await DbContext.ResourceServers.IgnoreQueryFilters().ToListAsync(),
            entity => Assert.Equal(TenantTwoId, entity.NormalizedTenantId)
        );
        Assert.All(
            await DbContext.Scopes.IgnoreQueryFilters().ToListAsync(),
            entity => Assert.Equal(TenantTwoId, entity.NormalizedTenantId)
        );
        Assert.All(
            await DbContext.ResourceServerSecrets.IgnoreQueryFilters().ToListAsync(),
            entity => Assert.Equal(TenantTwoId, entity.NormalizedTenantId)
        );
        Assert.All(
            await DbContext.RoleAssignments.IgnoreQueryFilters().ToListAsync(),
            entity => Assert.Equal(TenantTwoId, entity.NormalizedTenantId)
        );
        Assert.All(
            await DbContext.Secrets.ToListAsync(),
            entity => Assert.Contains(TenantTwoId, entity.SecretId)
        );
    }

    [Fact]
    public async Task RemoveAsync_OnARelationalProvider_WhenANonSystemResourceServerExists_ThrowsInvalidOperation()
    {
        await SeedTenantWithSystemRowsAsync(TenantOneId, CancellationToken.None);
        await SeedTenantWithSystemRowsAsync(TenantTwoId, CancellationToken.None);
        await SeedResourceServerAsync(TenantTwoId, isSystem: false, CancellationToken.None);

        var before = await CountRowsAsync(CancellationToken.None);
        Assert.Equal(new RowCounts(2, 4, 7, 3, 6, 3, 2), before);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Store.RemoveAsync(TenantTwoId, CancellationToken.None)
        );

        Assert.Equal(before, await CountRowsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task RemoveAsync_OnARelationalProvider_RemovesTheEndUserDataOfTheTenant()
    {
        await SeedTenantWithSystemRowsAsync(TenantOneId, CancellationToken.None);
        await SeedTenantWithSystemRowsAsync(TenantTwoId, CancellationToken.None);
        await SeedEndUserDataAsync(TenantOneId, CancellationToken.None);
        await SeedEndUserDataAsync(TenantTwoId, CancellationToken.None);

        Assert.Equal(2, await DbContext.Grants.IgnoreQueryFilters().CountAsync());
        Assert.Equal(2, await DbContext.FederatedPrincipals.IgnoreQueryFilters().CountAsync());
        Assert.Equal(2, await DbContext.FederatedIdentities.IgnoreQueryFilters().CountAsync());

        var removed = await Store.RemoveAsync(TenantOneId, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        Assert.True(removed);

        var grant = Assert.Single(await DbContext.Grants.IgnoreQueryFilters().ToListAsync());
        Assert.Equal(TenantTwoId, grant.NormalizedTenantId);

        var principal = Assert.Single(
            await DbContext.FederatedPrincipals.IgnoreQueryFilters().ToListAsync()
        );
        Assert.Equal(TenantTwoId, principal.NormalizedTenantId);

        var identity = Assert.Single(
            await DbContext.FederatedIdentities.IgnoreQueryFilters().ToListAsync()
        );
        Assert.Equal(TenantTwoId, identity.NormalizedTenantId);
    }

    [Fact]
    public async Task RemoveAsync_OnARelationalProvider_RemovesTheRoleAssignmentsItsPrincipalsHoldInOtherTenants()
    {
        await SeedTenantWithSystemRowsAsync(TenantOneId, CancellationToken.None);
        await SeedTenantWithSystemRowsAsync(TenantTwoId, CancellationToken.None);
        await SeedEndUserDataAsync(TenantOneId, CancellationToken.None);
        await RoleAssignmentStore.AddAsync(
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
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var removed = await Store.RemoveAsync(TenantOneId, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        Assert.True(removed);
        var remaining = await DbContext
            .RoleAssignments.IgnoreQueryFilters()
            .Select(entity => entity.AssignmentId)
            .OrderBy(assignmentId => assignmentId)
            .ToListAsync();
        Assert.Equal(["assign-tenant-2"], remaining);
    }

    [Fact]
    public async Task RemoveAsync_KeepsTheTenantsUsageRows()
    {
        await SeedTenantWithSystemRowsAsync(TenantOneId, CancellationToken.None);
        var day = new DateOnly(2026, 10, 21);
        var idGenerator = Provider.GetRequiredService<IIdGenerator<long>>();
        var usageStore = new TenantUsageStore(Mock.Of<IStoreProvider>(), idGenerator, DbContext);
        await usageStore.AddAsync(
            Guid.NewGuid(),
            1,
            [
                new PersistedTenantUsage
                {
                    TenantId = TenantOneId,
                    TeamPath = "/platform/",
                    Day = day,
                    Meter = "requests",
                    Count = 7,
                },
            ],
            CancellationToken.None
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var removed = await Store.RemoveAsync(TenantOneId, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        Assert.True(removed);
        Assert.Empty(await DbContext.Tenants.ToListAsync());
        var entries = await usageStore.ListByTeamAsync(
            "/platform/",
            day,
            day,
            CancellationToken.None
        );
        var entry = Assert.Single(entries);
        Assert.Equal(TenantOneId, entry.TenantId);
        Assert.Equal(7, entry.Count);
    }

    #endregion
}
