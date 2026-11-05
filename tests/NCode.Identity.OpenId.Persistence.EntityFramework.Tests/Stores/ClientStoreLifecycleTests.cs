// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using IdGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

public sealed class ClientStoreLifecycleTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string ClientId = "client-1";

    private readonly ServiceProvider _provider;
    private readonly OpenIdDbContext _dbContext;
    private readonly TenantStore _tenantStore;
    private readonly ClientStore _store;

    public ClientStoreLifecycleTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        );

        _provider = serviceCollection.BuildServiceProvider();
        _dbContext = _provider.GetRequiredService<OpenIdDbContext>();
        var idGenerator = _provider.GetRequiredService<IIdGenerator<long>>();
        _tenantStore = new TenantStore(Mock.Of<IStoreProvider>(), idGenerator, _dbContext);
        _store = new ClientStore(
            Mock.Of<IStoreProvider>(),
            idGenerator,
            _dbContext,
            TestCryptoServices.CreateResourceIdGenerator()
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

    private static PersistedClient CreateClient(params PersistedSecret[] secrets) =>
        new()
        {
            TenantId = TenantId,
            ClientId = ClientId,
            ConcurrencyToken = string.Empty,
            IsDisabled = false,
            Settings = new PersistedClientSettings
            {
                TenantId = TenantId,
                ClientId = ClientId,
                ConcurrencyToken = string.Empty,
                Value = JsonSerializer.SerializeToElement(new Dictionary<string, object>()),
            },
            Secrets = new PersistedClientSecrets
            {
                TenantId = TenantId,
                ClientId = ClientId,
                ConcurrencyToken = string.Empty,
                Value = secrets,
            },
        };

    private static PersistedRoleAssignment CreateAssignment(
        string assignmentId,
        string roleName,
        string resourceType,
        string resourceId,
        string principalId = "subject-1"
    ) =>
        new()
        {
            TenantId = TenantId,
            AssignmentId = assignmentId,
            PrincipalId = principalId,
            RoleName = roleName,
            ResourceType = resourceType,
            ResourceId = resourceId,
            ConcurrencyToken = string.Empty,
        };

    private async Task SeedTenantAsync()
    {
        _dbContext.SeedTeam(_provider.GetRequiredService<IIdGenerator<long>>(), "platform");
        var tenant = new PersistedTenant
        {
            TenantId = TenantId,
            TeamId = "platform",
            ConcurrencyToken = string.Empty,
            Issuer = null,
            IsDisabled = false,
            DisplayName = "Tenant One",
            Settings = new PersistedTenantSettings
            {
                TenantId = TenantId,
                ConcurrencyToken = string.Empty,
                Value = JsonSerializer.SerializeToElement(new Dictionary<string, object>()),
            },
            Secrets = new PersistedTenantSecrets
            {
                TenantId = TenantId,
                ConcurrencyToken = string.Empty,
                Value = [],
            },
        };

        await _tenantStore.AddAsync(tenant, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
    }

    private async Task SeedClientAsync(params PersistedSecret[] secrets)
    {
        await SeedTenantAsync();
        await _store.AddAsync(CreateClient(secrets), CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task AddAsync_AssignsRowConcurrencyToken()
    {
        await SeedTenantAsync();
        var client = CreateClient();

        await _store.AddAsync(client, CancellationToken.None);

        Assert.False(string.IsNullOrEmpty(client.ConcurrencyToken));
    }

    [Fact]
    public async Task AddAsync_AssignsAServerGeneratedPrincipalId()
    {
        await SeedTenantAsync();
        var client = CreateClient();
        client.PrincipalId = "caller-chosen";

        await _store.AddAsync(client, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        Assert.NotEqual("caller-chosen", client.PrincipalId);
        Assert.False(string.IsNullOrEmpty(client.PrincipalId));
        var reloaded = await _store.GetOrDefaultAsync(ClientId, CancellationToken.None);
        Assert.NotNull(reloaded);
        Assert.Equal(client.PrincipalId, reloaded.PrincipalId);
    }

    [Fact]
    public async Task RemoveAsync_WhenNoSecrets_RemovesAndReturnsTrue()
    {
        await SeedClientAsync();

        var removed = await _store.RemoveAsync(ClientId, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        Assert.True(removed);
        var reloaded = await _store.GetOrDefaultAsync(ClientId, CancellationToken.None);
        Assert.Null(reloaded);
    }

    [Fact]
    public async Task RemoveAsync_WhenHasSecrets_RemovesThemWithIt()
    {
        await SeedClientAsync(CreateSecret("secret-1"));

        var removed = await _store.RemoveAsync(ClientId, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        Assert.True(removed);
        var reloaded = await _store.GetOrDefaultAsync(ClientId, CancellationToken.None);
        Assert.Null(reloaded);
    }

    [Fact]
    public async Task RemoveAsync_WhenSecretsRemovedInSameUnitOfWork_RemovesAndReturnsTrue()
    {
        await SeedClientAsync(CreateSecret("secret-1"));

        await _store.RemoveSecretAsync(ClientId, "secret-1", CancellationToken.None);
        var removed = await _store.RemoveAsync(ClientId, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        Assert.True(removed);
        var reloaded = await _store.GetOrDefaultAsync(ClientId, CancellationToken.None);
        Assert.Null(reloaded);
    }

    [Fact]
    public async Task RemoveAsync_RemovesTheRoleAssignmentsAtTheClientsNode()
    {
        await SeedClientAsync();
        var roleAssignmentStore = new RoleAssignmentStore(
            Mock.Of<IStoreProvider>(),
            _provider.GetRequiredService<IIdGenerator<long>>(),
            _dbContext
        );
        await roleAssignmentStore.AddAsync(
            CreateAssignment("assign-1", "Owner", "client", "client-1"),
            CancellationToken.None
        );
        await roleAssignmentStore.AddAsync(
            CreateAssignment("assign-2", "Owner", "client", "client-2"),
            CancellationToken.None
        );
        await roleAssignmentStore.AddAsync(
            CreateAssignment("assign-3", "TenantAdmin", "tenant", TenantId),
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var removed = await _store.RemoveAsync(ClientId, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        Assert.True(removed);
        var remaining = await _dbContext
            .RoleAssignments.IgnoreQueryFilters()
            .Select(entity => entity.AssignmentId)
            .OrderBy(assignmentId => assignmentId)
            .ToListAsync();
        Assert.Equal(["assign-2", "assign-3"], remaining);
    }

    [Fact]
    public async Task RemoveAsync_RemovesTheRoleAssignmentsTheClientsPrincipalHolds()
    {
        await SeedClientAsync();
        var client = await _store.GetOrDefaultAsync(ClientId, CancellationToken.None);
        Assert.NotNull(client);
        var principalId = client.PrincipalId;
        var roleAssignmentStore = new RoleAssignmentStore(
            Mock.Of<IStoreProvider>(),
            _provider.GetRequiredService<IIdGenerator<long>>(),
            _dbContext
        );
        await roleAssignmentStore.AddAsync(
            CreateAssignment("assign-1", "TeamAdmin", "team", "acme", principalId),
            CancellationToken.None
        );
        await roleAssignmentStore.AddAsync(
            CreateAssignment("assign-2", "TenantAdmin", "tenant", TenantId),
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var removed = await _store.RemoveAsync(ClientId, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        Assert.True(removed);
        var remaining = await _dbContext
            .RoleAssignments.IgnoreQueryFilters()
            .Select(entity => entity.AssignmentId)
            .OrderBy(assignmentId => assignmentId)
            .ToListAsync();
        Assert.Equal(["assign-2"], remaining);
    }

    [Fact]
    public async Task RemoveAsync_WhenMissing_ReturnsFalse()
    {
        var removed = await _store.RemoveAsync("missing", CancellationToken.None);

        Assert.False(removed);
    }
}
