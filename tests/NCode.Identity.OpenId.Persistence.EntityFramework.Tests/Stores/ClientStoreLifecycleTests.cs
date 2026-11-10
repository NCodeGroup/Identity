// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using IdGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

public sealed class ClientStoreLifecycleTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string ClientId = "client-1";

    private ServiceProvider Provider { get; }
    private OpenIdDbContext DbContext { get; }
    private TenantStore TenantStore { get; }
    private ClientStore Store { get; }

    public ClientStoreLifecycleTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        );

        Provider = serviceCollection.BuildServiceProvider();
        DbContext = Provider.GetRequiredService<OpenIdDbContext>();
        var idGenerator = Provider.GetRequiredService<IIdGenerator<long>>();
        TenantStore = new TenantStore(Mock.Of<IStoreProvider>(), idGenerator, DbContext);
        Store = new ClientStore(
            Mock.Of<IStoreProvider>(),
            idGenerator,
            DbContext,
            TestCryptoServices.CreateResourceIdGenerator()
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

    private static PersistedClient CreateClient(params PersistedSecret[] secrets) =>
        new PersistedClientBuilder
        {
            TenantId = TenantId,
            ClientId = ClientId,
            Secrets = secrets,
        }.Build();

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
        DbContext.SeedTeam(Provider.GetRequiredService<IIdGenerator<long>>(), "platform");
        var tenant = new PersistedTenantBuilder
        {
            TenantId = TenantId,
            DisplayName = "Tenant One",
        }.Build();

        await TenantStore.AddAsync(tenant, CancellationToken.None);
        await DbContext.SaveChangesAsync();
    }

    private async Task SeedClientAsync(params PersistedSecret[] secrets)
    {
        await SeedTenantAsync();
        await Store.AddAsync(CreateClient(secrets), CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task AddAsync_AssignsRowConcurrencyToken()
    {
        await SeedTenantAsync();
        var client = CreateClient();

        await Store.AddAsync(client, CancellationToken.None);

        Assert.False(string.IsNullOrEmpty(client.ConcurrencyToken));
    }

    [Fact]
    public async Task AddAsync_AssignsAServerGeneratedPrincipalId()
    {
        await SeedTenantAsync();
        var client = CreateClient();
        client.PrincipalId = "caller-chosen";

        await Store.AddAsync(client, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        Assert.NotEqual("caller-chosen", client.PrincipalId);
        Assert.False(string.IsNullOrEmpty(client.PrincipalId));
        var reloaded = await Store.GetOrDefaultAsync(ClientId, CancellationToken.None);
        Assert.NotNull(reloaded);
        Assert.Equal(client.PrincipalId, reloaded.PrincipalId);
    }

    [Fact]
    public async Task AddAsync_WithAnOwningNode_ReadsItBack()
    {
        await SeedTenantAsync();
        var client = new PersistedClientBuilder
        {
            TenantId = TenantId,
            ClientId = ClientId,
            OwningNodeType = "team",
            OwningNodeId = "Team-One",
        }.Build();

        await Store.AddAsync(client, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var reloaded = await Store.GetOrDefaultAsync(ClientId, CancellationToken.None);
        Assert.NotNull(reloaded);
        Assert.Equal("team", reloaded.OwningNodeType);
        Assert.Equal("Team-One", reloaded.OwningNodeId);
    }

    [Fact]
    public async Task RemoveAsync_WhenNoSecrets_RemovesAndReturnsTrue()
    {
        await SeedClientAsync();

        var removed = await Store.RemoveAsync(ClientId, CancellationToken.None);
        await DbContext.SaveChangesAsync();

        Assert.True(removed);
        var reloaded = await Store.GetOrDefaultAsync(ClientId, CancellationToken.None);
        Assert.Null(reloaded);
    }

    [Fact]
    public async Task RemoveAsync_WhenHasSecrets_RemovesThemWithIt()
    {
        await SeedClientAsync(CreateSecret("secret-1"));

        var removed = await Store.RemoveAsync(ClientId, CancellationToken.None);
        await DbContext.SaveChangesAsync();

        Assert.True(removed);
        var reloaded = await Store.GetOrDefaultAsync(ClientId, CancellationToken.None);
        Assert.Null(reloaded);
    }

    [Fact]
    public async Task RemoveAsync_WhenSecretsRemovedInSameUnitOfWork_RemovesAndReturnsTrue()
    {
        await SeedClientAsync(CreateSecret("secret-1"));

        await Store.RemoveSecretAsync(ClientId, "secret-1", CancellationToken.None);
        var removed = await Store.RemoveAsync(ClientId, CancellationToken.None);
        await DbContext.SaveChangesAsync();

        Assert.True(removed);
        var reloaded = await Store.GetOrDefaultAsync(ClientId, CancellationToken.None);
        Assert.Null(reloaded);
    }

    [Fact]
    public async Task RemoveAsync_RemovesTheRoleAssignmentsAtTheClientsNode()
    {
        await SeedClientAsync();
        var roleAssignmentStore = new RoleAssignmentStore(
            Mock.Of<IStoreProvider>(),
            Provider.GetRequiredService<IIdGenerator<long>>(),
            DbContext
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
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var removed = await Store.RemoveAsync(ClientId, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        Assert.True(removed);
        var remaining = await DbContext
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
        var client = await Store.GetOrDefaultAsync(ClientId, CancellationToken.None);
        Assert.NotNull(client);
        var principalId = client.PrincipalId;
        var roleAssignmentStore = new RoleAssignmentStore(
            Mock.Of<IStoreProvider>(),
            Provider.GetRequiredService<IIdGenerator<long>>(),
            DbContext
        );
        await roleAssignmentStore.AddAsync(
            CreateAssignment("assign-1", "TeamAdmin", "team", "acme", principalId),
            CancellationToken.None
        );
        await roleAssignmentStore.AddAsync(
            CreateAssignment("assign-2", "TenantAdmin", "tenant", TenantId),
            CancellationToken.None
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var removed = await Store.RemoveAsync(ClientId, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        Assert.True(removed);
        var remaining = await DbContext
            .RoleAssignments.IgnoreQueryFilters()
            .Select(entity => entity.AssignmentId)
            .OrderBy(assignmentId => assignmentId)
            .ToListAsync();
        Assert.Equal(["assign-2"], remaining);
    }

    [Fact]
    public async Task RemoveAsync_WhenMissing_ReturnsFalse()
    {
        var removed = await Store.RemoveAsync("missing", CancellationToken.None);

        Assert.False(removed);
    }
}
