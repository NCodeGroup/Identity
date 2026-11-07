// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using IdGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Secrets.KeyWrapping;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

public sealed class TenantStoreLifecycleTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string ClientId = "client-1";

    private readonly ServiceProvider _provider;
    private readonly OpenIdDbContext _dbContext;
    private readonly TenantStore _store;
    private readonly ClientStore _clientStore;
    private readonly ResourceServerStore _resourceServerStore;
    private readonly RoleAssignmentStore _roleAssignmentStore;
    private readonly MasterKeyStore _masterKeyStore;
    private readonly OwnerRootKeyStore _ownerRootKeyStore;

    public TenantStoreLifecycleTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        );

        _provider = serviceCollection.BuildServiceProvider();
        _dbContext = _provider.GetRequiredService<OpenIdDbContext>();
        var idGenerator = _provider.GetRequiredService<IIdGenerator<long>>();
        _store = new TenantStore(Mock.Of<IStoreProvider>(), idGenerator, _dbContext);
        _dbContext.SeedTeam(_provider.GetRequiredService<IIdGenerator<long>>(), "platform");
        _clientStore = new ClientStore(
            Mock.Of<IStoreProvider>(),
            idGenerator,
            _dbContext,
            TestCryptoServices.CreateResourceIdGenerator()
        );
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
        _masterKeyStore = new MasterKeyStore(Mock.Of<IStoreProvider>(), idGenerator, _dbContext);
        _ownerRootKeyStore = new OwnerRootKeyStore(
            Mock.Of<IStoreProvider>(),
            idGenerator,
            _dbContext
        );
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _provider.Dispose();
    }

    private static PersistedSecret CreateSecret(string secretId) =>
        new()
        {
            SecretId = secretId,
            ConcurrencyToken = string.Empty,
            Use = "sig",
            Algorithm = "RS256",
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
            ActivatesWhen = DateTimeOffset.UnixEpoch,
            RetiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
            SecretType = SecretTypes.Symmetric,
            SecretEncoding = NCode.Identity.Secrets.SecretEncodings.SymmetricRaw,
            Origin = SecretOrigins.Generated,
            KeySizeBits = 256,
            EncodedValue = "encoded-value",
        };

    private static PersistedTenant CreateTenant(params PersistedSecret[] secrets) =>
        CreateTenant(TenantId, secrets);

    private static PersistedTenant CreateTenant(string tenantId, PersistedSecret[] secrets) =>
        new()
        {
            TenantId = tenantId,
            TeamId = "platform",
            ConcurrencyToken = string.Empty,
            Issuer = null,
            IsDisabled = false,
            DisplayName = "Tenant One",
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

    private async Task SeedTenantAsync(params PersistedSecret[] secrets)
    {
        await _store.AddAsync(CreateTenant(secrets), CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    private async Task SeedClientAsync()
    {
        var client = new PersistedClientBuilder
        {
            TenantId = TenantId,
            ClientId = ClientId,
        }.Build();

        await _clientStore.AddAsync(client, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    private async Task SeedResourceServerAsync(bool isSystem)
    {
        const string resourceServerId = "rs-1";

        var resourceServer = new PersistedResourceServer
        {
            TenantId = TenantId,
            ResourceServerId = resourceServerId,
            Identifier = isSystem ? "urn:ncode:openid" : "https://api.example.com",
            ConcurrencyToken = string.Empty,
            Name = "Resource Server One",
            IsSystem = isSystem,
            IsDisabled = false,
            Settings = new PersistedResourceServerSettings
            {
                TenantId = TenantId,
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
            ],
            Secrets = new PersistedResourceServerSecrets
            {
                TenantId = TenantId,
                ResourceServerId = resourceServerId,
                Value = [],
            },
        };

        await _resourceServerStore.AddAsync(resourceServer, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    private async Task SeedRoleAssignmentAsync()
    {
        await _roleAssignmentStore.AddAsync(
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
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
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
            await _masterKeyStore.AddAsync(masterKey, CancellationToken.None);

        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task AddAsync_AssignsRowConcurrencyToken()
    {
        var tenant = CreateTenant();

        await _store.AddAsync(tenant, CancellationToken.None);

        Assert.False(string.IsNullOrEmpty(tenant.ConcurrencyToken));
    }

    [Fact]
    public async Task GetManyAsync_ReturnsTheExistingTenantsOrderedAndSkipsTheMissing()
    {
        await _store.AddAsync(CreateTenant(CreateSecret("secret-1")), CancellationToken.None);
        var second = CreateTenant("tenant-2", []);
        await _store.AddAsync(second, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var tenants = await _store.GetManyAsync(
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
        var tenant = Assert.Single(await _store.GetManyAsync([TenantId], CancellationToken.None));

        var tokens = await _store.GetConcurrencyTokensAsync(
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

        var removed = await _store.RemoveAsync(TenantId, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        Assert.True(removed);
        var reloaded = await _store.GetOrDefaultAsync(TenantId, CancellationToken.None);
        Assert.Null(reloaded);
    }

    [Fact]
    public async Task RemoveAsync_WhenTheTenantHoldsSecrets_RemovesThemWithIt()
    {
        await SeedTenantAsync(CreateSecret("secret-1"), CreateSecret("secret-2"));

        var removed = await _store.RemoveAsync(TenantId, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        Assert.True(removed);
        Assert.Empty(await _dbContext.TenantSecrets.ToListAsync());
        Assert.Empty(await _dbContext.Secrets.ToListAsync());
        Assert.Empty(await _dbContext.Tenants.ToListAsync());
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

        var removed = await _store.RemoveAsync(TenantId, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        Assert.True(removed);
        var remainingKeyIds = await _dbContext
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
            await _ownerRootKeyStore.AddAsync(
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

        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var removed = await _store.RemoveAsync(TenantId, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        Assert.True(removed);
        var remaining = Assert.Single(await _dbContext.OwnerRootKeys.AsNoTracking().ToListAsync());
        Assert.Equal("tenant-2", remaining.OwnerId);
    }

    [Fact]
    public async Task RemoveAsync_WhenTheTenantHoldsASystemResourceServerAndOwners_RemovesThemWithIt()
    {
        await SeedTenantAsync();
        await SeedResourceServerAsync(isSystem: true);
        await SeedRoleAssignmentAsync();

        var removed = await _store.RemoveAsync(TenantId, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        Assert.True(removed);
        Assert.Empty(await _dbContext.ResourceServers.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await _dbContext.Scopes.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await _dbContext.ResourceServerSecrets.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await _dbContext.RoleAssignments.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await _dbContext.Tenants.ToListAsync());
    }

    [Fact]
    public async Task RemoveAsync_WhenHasANonSystemResourceServer_Throws()
    {
        await SeedTenantAsync();
        await SeedResourceServerAsync(isSystem: false);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _store.RemoveAsync(TenantId, CancellationToken.None)
        );
    }

    [Fact]
    public async Task RemoveAsync_WhenHasClients_Throws()
    {
        await SeedTenantAsync();
        await SeedClientAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _store.RemoveAsync(TenantId, CancellationToken.None)
        );
    }

    [Fact]
    public async Task RemoveAsync_WhenMissing_ReturnsFalse()
    {
        var removed = await _store.RemoveAsync("missing", CancellationToken.None);

        Assert.False(removed);
    }

    [Fact]
    public async Task HasDependentsAsync_WhenNoDependents_ReturnsFalse()
    {
        await SeedTenantAsync();

        var result = await _store.HasDependentsAsync(TenantId, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task HasDependentsAsync_WhenHasClients_ReturnsTrue()
    {
        await SeedTenantAsync();
        await SeedClientAsync();

        var result = await _store.HasDependentsAsync(TenantId, CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task HasDependentsAsync_WhenTheTenantHoldsOnlySecrets_ReturnsFalse()
    {
        await SeedTenantAsync(CreateSecret("secret-1"));

        var result = await _store.HasDependentsAsync(TenantId, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task HasDependentsAsync_WhenHasANonSystemResourceServer_ReturnsTrue()
    {
        await SeedTenantAsync();
        await SeedResourceServerAsync(isSystem: false);

        var result = await _store.HasDependentsAsync(TenantId, CancellationToken.None);

        Assert.True(result);
    }
}
