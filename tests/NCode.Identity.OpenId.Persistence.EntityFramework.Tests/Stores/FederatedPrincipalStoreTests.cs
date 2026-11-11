// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using IdGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

public sealed class FederatedPrincipalStoreTests : IDisposable
{
    private const string TenantId = "tenant-1";

    private ServiceProvider Provider { get; }
    private OpenIdDbContext DbContext { get; }
    private FederatedPrincipalStore Store { get; }

    public FederatedPrincipalStoreTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        );

        Provider = serviceCollection.BuildServiceProvider();
        DbContext = Provider.GetRequiredService<OpenIdDbContext>();
        var idGenerator = Provider.GetRequiredService<IIdGenerator<long>>();
        Store = new FederatedPrincipalStore(Mock.Of<IStoreProvider>(), idGenerator, DbContext);
        DbContext.SeedTenant(idGenerator, TenantId);
    }

    public void Dispose()
    {
        DbContext.Dispose();
        Provider.Dispose();
    }

    private static PersistedFederatedPrincipal CreatePrincipal(string principalId) =>
        new()
        {
            TenantId = TenantId,
            PrincipalId = principalId,
            ConcurrencyToken = string.Empty,
        };

    private static PersistedRoleAssignment CreateAssignment(
        string assignmentId,
        string tenantId,
        string principalId
    ) =>
        new()
        {
            TenantId = tenantId,
            AssignmentId = assignmentId,
            PrincipalId = principalId,
            RoleName = "Owner",
            ResourceType = "tenant",
            ResourceId = tenantId,
            ConcurrencyToken = string.Empty,
        };

    private static PersistedClient CreateClient(string clientId) =>
        new PersistedClientBuilder { TenantId = TenantId, ClientId = clientId }.Build();

    private static PersistedConsent CreateConsent(string clientId, string subjectId) =>
        new()
        {
            TenantId = TenantId,
            ClientId = clientId,
            SubjectId = subjectId,
            RequestedScopes = ["openid"],
            GrantedScopes = ["openid"],
            CreatedWhen = DateTimeOffset.UnixEpoch,
            UpdatedWhen = DateTimeOffset.UnixEpoch,
            ConcurrencyToken = string.Empty,
        };

    private static PersistedGrant CreateGrant(string clientId, string subjectId) =>
        new()
        {
            GrantType = "authorization_code",
            HashedKey = $"{subjectId}-hashed",
            GrantId = $"{subjectId}-grant",
            ConcurrencyToken = string.Empty,
            TenantId = TenantId,
            ClientId = clientId,
            SubjectId = subjectId,
            SessionId = null,
            RefreshTokenLineageId = null,
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddHours(1),
            RevokedWhen = null,
            ConsumedWhen = null,
            ProtectedPayload = "protected-payload",
        };

    [Fact]
    public async Task AddAsync_WhenPersisted_RoundTripsByPrincipalId()
    {
        await Store.AddAsync(CreatePrincipal("principal-1"), CancellationToken.None);
        await DbContext.SaveChangesAsync();

        var loaded = await Store.GetOrDefaultAsync("principal-1", CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal("principal-1", loaded.PrincipalId);
        Assert.NotEmpty(loaded.ConcurrencyToken);
    }

    [Fact]
    public async Task GetOrDefaultAsync_WhenCaseDiffers_IsCaseInsensitive()
    {
        await Store.AddAsync(CreatePrincipal("Principal-1"), CancellationToken.None);
        await DbContext.SaveChangesAsync();

        var loaded = await Store.GetOrDefaultAsync("principal-1", CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal("Principal-1", loaded.PrincipalId);
    }

    [Fact]
    public async Task GetOrDefaultAsync_WhenMissing_ReturnsNull()
    {
        var loaded = await Store.GetOrDefaultAsync("nope", CancellationToken.None);

        Assert.Null(loaded);
    }

    [Fact]
    public async Task AddAsync_WhenBlocked_RoundTripsTheBlockState()
    {
        var principal = CreatePrincipal("principal-1");
        principal.IsBlocked = true;
        await Store.AddAsync(principal, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var loaded = await Store.GetOrDefaultAsync("principal-1", CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.True(loaded.IsBlocked);
    }

    [Fact]
    public async Task UpdateAsync_WhenBlocked_PersistsTheBlockState()
    {
        await Store.AddAsync(CreatePrincipal("principal-1"), CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var loaded = await Store.GetOrDefaultAsync("principal-1", CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.False(loaded.IsBlocked);
        loaded.IsBlocked = true;
        await Store.UpdateAsync(loaded, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var reloaded = await Store.GetOrDefaultAsync("principal-1", CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.True(reloaded.IsBlocked);
    }

    #region RemoveAsync Tests

    [Fact]
    public async Task RemoveAsync_RemovesTheRoleAssignmentsOfThePrincipalInEveryTenant()
    {
        var idGenerator = Provider.GetRequiredService<IIdGenerator<long>>();
        DbContext.SeedTenant(idGenerator, "tenant-2");
        await Store.AddAsync(CreatePrincipal("principal-1"), CancellationToken.None);
        await Store.AddAsync(CreatePrincipal("principal-2"), CancellationToken.None);
        var roleAssignmentStore = new RoleAssignmentStore(
            Mock.Of<IStoreProvider>(),
            idGenerator,
            DbContext
        );
        await roleAssignmentStore.AddAsync(
            CreateAssignment("assign-1", TenantId, "principal-1"),
            CancellationToken.None
        );
        await roleAssignmentStore.AddAsync(
            CreateAssignment("assign-2", "tenant-2", "Principal-1"),
            CancellationToken.None
        );
        await roleAssignmentStore.AddAsync(
            CreateAssignment("assign-3", TenantId, "principal-2"),
            CancellationToken.None
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        await Store.RemoveAsync("principal-1", CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var remaining = await DbContext
            .RoleAssignments.IgnoreQueryFilters()
            .Select(entity => entity.AssignmentId)
            .OrderBy(assignmentId => assignmentId)
            .ToListAsync();
        Assert.Equal(["assign-3"], remaining);
        Assert.Null(await Store.GetOrDefaultAsync("principal-1", CancellationToken.None));
    }

    [Fact]
    public async Task RemoveAsync_RemovesTheConsentsAndGrantsOfThePrincipal()
    {
        var idGenerator = Provider.GetRequiredService<IIdGenerator<long>>();
        const string clientId = "client-1";
        await Store.AddAsync(CreatePrincipal("principal-1"), CancellationToken.None);
        await Store.AddAsync(CreatePrincipal("principal-2"), CancellationToken.None);
        var clientStore = new ClientStore(
            Mock.Of<IStoreProvider>(),
            idGenerator,
            DbContext,
            TestCryptoServices.CreateResourceIdGenerator()
        );
        await clientStore.AddAsync(CreateClient(clientId), CancellationToken.None);
        await DbContext.SaveChangesAsync();
        var consentStore = new ConsentStore(Mock.Of<IStoreProvider>(), idGenerator, DbContext);
        await consentStore.SaveAsync(
            CreateConsent(clientId, "principal-1"),
            CancellationToken.None
        );
        await consentStore.SaveAsync(
            CreateConsent(clientId, "principal-2"),
            CancellationToken.None
        );
        var grantStore = new GrantStore(Mock.Of<IStoreProvider>(), idGenerator, DbContext);
        await grantStore.AddAsync(CreateGrant(clientId, "principal-1"), CancellationToken.None);
        await grantStore.AddAsync(CreateGrant(clientId, "principal-2"), CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        await Store.RemoveAsync("principal-1", CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var remainingConsents = await DbContext
            .Consents.IgnoreQueryFilters()
            .Select(entity => entity.SubjectId)
            .ToListAsync();
        var remainingGrants = await DbContext
            .Grants.IgnoreQueryFilters()
            .Select(entity => entity.SubjectId)
            .ToListAsync();
        Assert.Equal(["principal-2"], remainingConsents);
        Assert.Equal(["principal-2"], remainingGrants);
    }

    #endregion
}
