// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using IdGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Secrets.KeyWrapping;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

public sealed class TenantStoreLifecycleTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string ClientId = "client-1";

    private ServiceProvider Provider { get; }
    private OpenIdDbContext DbContext { get; }
    private TenantStore Store { get; }
    private ClientStore ClientStore { get; }
    private ResourceServerStore ResourceServerStore { get; }
    private RoleAssignmentStore RoleAssignmentStore { get; }
    private MasterKeyStore MasterKeyStore { get; }
    private OwnerRootKeyStore OwnerRootKeyStore { get; }

    public TenantStoreLifecycleTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        );

        Provider = serviceCollection.BuildServiceProvider();
        DbContext = Provider.GetRequiredService<OpenIdDbContext>();
        var idGenerator = Provider.GetRequiredService<IIdGenerator<long>>();
        Store = new TenantStore(Mock.Of<IStoreProvider>(), idGenerator, DbContext);
        DbContext.SeedTeam(Provider.GetRequiredService<IIdGenerator<long>>(), "platform");
        ClientStore = new ClientStore(
            Mock.Of<IStoreProvider>(),
            idGenerator,
            DbContext,
            TestCryptoServices.CreateResourceIdGenerator()
        );
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
        MasterKeyStore = new MasterKeyStore(Mock.Of<IStoreProvider>(), idGenerator, DbContext);
        OwnerRootKeyStore = new OwnerRootKeyStore(
            Mock.Of<IStoreProvider>(),
            idGenerator,
            DbContext
        );
    }

    public void Dispose()
    {
        DbContext.Dispose();
        Provider.Dispose();
    }

    private static PersistedSecret CreateSecret(string secretId) =>
        new PersistedSecretBuilder
        {
            SecretId = secretId,
            Use = "sig",
            Algorithm = "RS256",
            EncodedValue = "encoded-value",
        }.Build();

    private static PersistedTenant CreateTenant(params PersistedSecret[] secrets) =>
        CreateTenant(TenantId, secrets);

    private static PersistedTenant CreateTenant(string tenantId, PersistedSecret[] secrets) =>
        new PersistedTenantBuilder
        {
            TenantId = tenantId,
            DisplayName = "Tenant One",
            Secrets = secrets,
        }.Build();

    private async Task SeedTenantAsync(params PersistedSecret[] secrets)
    {
        await Store.AddAsync(CreateTenant(secrets), CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
    }

    private async Task SeedClientAsync()
    {
        var client = new PersistedClientBuilder
        {
            TenantId = TenantId,
            ClientId = ClientId,
        }.Build();

        await ClientStore.AddAsync(client, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
    }

    private async Task SeedResourceServerAsync(bool isSystem)
    {
        const string resourceServerId = "rs-1";

        var resourceServer = new PersistedResourceServerBuilder
        {
            TenantId = TenantId,
            ResourceServerId = resourceServerId,
            Identifier = isSystem ? "urn:ncode:openid" : "https://api.example.com",
            Name = "Resource Server One",
            IsSystem = isSystem,
            Scopes =
            [
                new PersistedScope
                {
                    Value = "openid",
                    Description = "Sign you in and issue an identity token.",
                    IsSystem = isSystem,
                },
            ],
        }.Build();

        await ResourceServerStore.AddAsync(resourceServer, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
    }

    private async Task SeedRoleAssignmentAsync()
    {
        await RoleAssignmentStore.AddAsync(
            new PersistedRoleAssignment
            {
                TenantId = TenantId,
                AssignmentId = "assign-1",
                PrincipalId = "subject-1",
                RoleName = "Owner",
                ResourceType = "client",
                ResourceId = ClientId,
                ConcurrencyToken = string.Empty,
            },
            CancellationToken.None
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
    }

    private static PersistedMasterKey CreateMasterKey(
        string ownerType,
        string ownerId,
        int version
    ) =>
        new()
        {
            KeyId = Guid.NewGuid(),
            OwnerType = ownerType,
            OwnerId = ownerId,
            Version = version,
            CreatedWhen = DateTimeOffset.UnixEpoch,
            RetiredWhen = null,
            RootKeyProvider = KeyWrapperNames.Local,
            RootKeyId = $"root-key-{version}",
            RootWrappedKey = $"root-wrapped-key-{version}",
            RecoveryKeyId = null,
            RecoveryWrappedKey = null,
            KeyCheck = $"key-check-{version}",
        };

    private async Task SeedMasterKeysAsync(params PersistedMasterKey[] masterKeys)
    {
        foreach (var masterKey in masterKeys)
            await MasterKeyStore.AddAsync(masterKey, CancellationToken.None);

        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task AddAsync_AssignsRowConcurrencyToken()
    {
        var tenant = CreateTenant();

        await Store.AddAsync(tenant, CancellationToken.None);

        Assert.False(string.IsNullOrEmpty(tenant.ConcurrencyToken));
    }

    [Fact]
    public async Task GetManyAsync_ReturnsTheExistingTenantsOrderedAndSkipsTheMissing()
    {
        await Store.AddAsync(CreateTenant(CreateSecret("secret-1")), CancellationToken.None);
        var second = CreateTenant("tenant-2", []);
        await Store.AddAsync(second, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var tenants = await Store.GetManyAsync(
            ["TENANT-2", TenantId.ToUpperInvariant(), "missing-tenant"],
            CancellationToken.None
        );

        Assert.Equal([TenantId, "tenant-2"], tenants.Select(tenant => tenant.TenantId));
        Assert.All(tenants, tenant => Assert.Equal("platform", tenant.TeamId));
        Assert.Single(tenants[0].Secrets.Value);
    }

    [Fact]
    public async Task GetConcurrencyTokensAsync_ReturnsTheRowTokenOfEachExistingTenant()
    {
        await SeedTenantAsync();
        var tenant = Assert.Single(await Store.GetManyAsync([TenantId], CancellationToken.None));

        var tokens = await Store.GetConcurrencyTokensAsync(
            [TenantId.ToUpperInvariant(), "missing-tenant"],
            CancellationToken.None
        );

        Assert.Single(tokens);
        Assert.Equal(tenant.ConcurrencyToken, tokens[TenantId]);
    }

    [Fact]
    public async Task RemoveAsync_WhenNoDependents_RemovesAndReturnsTrue()
    {
        await SeedTenantAsync();

        var removed = await Store.RemoveAsync(TenantId, CancellationToken.None);
        await DbContext.SaveChangesAsync();

        Assert.True(removed);
        var reloaded = await Store.GetOrDefaultAsync(TenantId, CancellationToken.None);
        Assert.Null(reloaded);
    }

    [Fact]
    public async Task RemoveAsync_WhenTheTenantHoldsSecrets_RemovesThemWithIt()
    {
        await SeedTenantAsync(CreateSecret("secret-1"), CreateSecret("secret-2"));

        var removed = await Store.RemoveAsync(TenantId, CancellationToken.None);
        await DbContext.SaveChangesAsync();

        Assert.True(removed);
        Assert.Empty(await DbContext.TenantSecrets.ToListAsync());
        Assert.Empty(await DbContext.Secrets.ToListAsync());
        Assert.Empty(await DbContext.Tenants.ToListAsync());
    }

    [Fact]
    public async Task RemoveAsync_TenantWithMasterKeys_RemovesThemAndKeepsOthers()
    {
        await SeedTenantAsync();
        var otherTenantKey = CreateMasterKey(SecretOwnerTypes.Tenant, "tenant-2", 1);
        // The platform key's owner id matches the tenant's on purpose: only the owner type keeps it.
        var platformKey = CreateMasterKey(SecretOwnerTypes.Platform, TenantId, 1);
        await SeedMasterKeysAsync(
            CreateMasterKey(SecretOwnerTypes.Tenant, TenantId, 1),
            CreateMasterKey(SecretOwnerTypes.Tenant, TenantId.ToUpperInvariant(), 2),
            otherTenantKey,
            platformKey
        );

        var removed = await Store.RemoveAsync(TenantId, CancellationToken.None);
        await DbContext.SaveChangesAsync();

        Assert.True(removed);
        var remainingKeyIds = await DbContext
            .MasterKeys.AsNoTracking()
            .Select(entity => entity.KeyId)
            .ToListAsync();
        Assert.Equal(2, remainingKeyIds.Count);
        Assert.Contains(otherTenantKey.KeyId, remainingKeyIds);
        Assert.Contains(platformKey.KeyId, remainingKeyIds);
    }

    [Fact]
    public async Task RemoveAsync_TenantWithAnOwnerRootKey_RemovesItAndKeepsOthers()
    {
        await SeedTenantAsync();
        foreach (var owner in new[] { TenantId.ToUpperInvariant(), "tenant-2" })
        {
            await OwnerRootKeyStore.AddAsync(
                new PersistedOwnerRootKey
                {
                    OwnerType = SecretOwnerTypes.Tenant,
                    OwnerId = owner,
                    KeyWrapperName = "aws-kms",
                    KeyReference = "arn:aws:kms:us-west-2:111122223333:key/abcd",
                    State = OwnerRootKeyStates.Active,
                    CreatedWhen = DateTimeOffset.UnixEpoch,
                    StateChangedWhen = DateTimeOffset.UnixEpoch,
                },
                CancellationToken.None
            );
        }

        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var removed = await Store.RemoveAsync(TenantId, CancellationToken.None);
        await DbContext.SaveChangesAsync();

        Assert.True(removed);
        var remaining = Assert.Single(await DbContext.OwnerRootKeys.AsNoTracking().ToListAsync());
        Assert.Equal("tenant-2", remaining.OwnerId);
    }

    [Fact]
    public async Task RemoveAsync_WhenTheTenantHoldsASystemResourceServerAndOwners_RemovesThemWithIt()
    {
        await SeedTenantAsync();
        await SeedResourceServerAsync(isSystem: true);
        await SeedRoleAssignmentAsync();

        var removed = await Store.RemoveAsync(TenantId, CancellationToken.None);
        await DbContext.SaveChangesAsync();

        Assert.True(removed);
        Assert.Empty(await DbContext.ResourceServers.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await DbContext.Scopes.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await DbContext.ResourceServerSecrets.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await DbContext.RoleAssignments.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await DbContext.Tenants.ToListAsync());
    }

    [Fact]
    public async Task RemoveAsync_WhenHasANonSystemResourceServer_Throws()
    {
        await SeedTenantAsync();
        await SeedResourceServerAsync(isSystem: false);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Store.RemoveAsync(TenantId, CancellationToken.None)
        );
    }

    [Fact]
    public async Task RemoveAsync_WhenHasClients_Throws()
    {
        await SeedTenantAsync();
        await SeedClientAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Store.RemoveAsync(TenantId, CancellationToken.None)
        );
    }

    [Fact]
    public async Task RemoveAsync_WhenMissing_ReturnsFalse()
    {
        var removed = await Store.RemoveAsync("missing", CancellationToken.None);

        Assert.False(removed);
    }

    [Fact]
    public async Task HasDependentsAsync_WhenNoDependents_ReturnsFalse()
    {
        await SeedTenantAsync();

        var result = await Store.HasDependentsAsync(TenantId, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task HasDependentsAsync_WhenHasClients_ReturnsTrue()
    {
        await SeedTenantAsync();
        await SeedClientAsync();

        var result = await Store.HasDependentsAsync(TenantId, CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task HasDependentsAsync_WhenTheTenantHoldsOnlySecrets_ReturnsFalse()
    {
        await SeedTenantAsync(CreateSecret("secret-1"));

        var result = await Store.HasDependentsAsync(TenantId, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task HasDependentsAsync_WhenHasANonSystemResourceServer_ReturnsTrue()
    {
        await SeedTenantAsync();
        await SeedResourceServerAsync(isSystem: false);

        var result = await Store.HasDependentsAsync(TenantId, CancellationToken.None);

        Assert.True(result);
    }
}
