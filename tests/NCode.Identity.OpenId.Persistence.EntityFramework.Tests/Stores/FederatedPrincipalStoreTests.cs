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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

public sealed class FederatedPrincipalStoreTests : IDisposable
{
    private const string TenantId = "tenant-1";

    private readonly ServiceProvider _provider;
    private readonly OpenIdDbContext _dbContext;
    private readonly FederatedPrincipalStore _store;

    public FederatedPrincipalStoreTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        );

        _provider = serviceCollection.BuildServiceProvider();
        _dbContext = _provider.GetRequiredService<OpenIdDbContext>();
        var idGenerator = _provider.GetRequiredService<IIdGenerator<long>>();
        _store = new FederatedPrincipalStore(Mock.Of<IStoreProvider>(), idGenerator, _dbContext);
        _dbContext.SeedTenant(idGenerator, TenantId);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _provider.Dispose();
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

    private static JsonElement EmptyObject() =>
        JsonSerializer.SerializeToElement(new Dictionary<string, object>());

    private static PersistedClient CreateClient(string clientId) =>
        new()
        {
            TenantId = TenantId,
            ClientId = clientId,
            ConcurrencyToken = string.Empty,
            IsDisabled = false,
            Settings = new PersistedClientSettings
            {
                TenantId = TenantId,
                ClientId = clientId,
                ConcurrencyToken = string.Empty,
                Value = EmptyObject(),
            },
            Secrets = new PersistedClientSecrets
            {
                TenantId = TenantId,
                ClientId = clientId,
                ConcurrencyToken = string.Empty,
                Value = [],
            },
        };

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
            PayloadJson = EmptyObject(),
        };

    [Fact]
    public async Task AddAsync_WhenPersisted_RoundTripsByPrincipalId()
    {
        await _store.AddAsync(CreatePrincipal("principal-1"), CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        var loaded = await _store.GetOrDefaultAsync("principal-1", CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal("principal-1", loaded.PrincipalId);
        Assert.NotEmpty(loaded.ConcurrencyToken);
    }

    [Fact]
    public async Task GetOrDefaultAsync_WhenCaseDiffers_IsCaseInsensitive()
    {
        await _store.AddAsync(CreatePrincipal("Principal-1"), CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        var loaded = await _store.GetOrDefaultAsync("principal-1", CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal("Principal-1", loaded.PrincipalId);
    }

    [Fact]
    public async Task GetOrDefaultAsync_WhenMissing_ReturnsNull()
    {
        var loaded = await _store.GetOrDefaultAsync("nope", CancellationToken.None);

        Assert.Null(loaded);
    }

    [Fact]
    public async Task AddAsync_WhenBlocked_RoundTripsTheBlockState()
    {
        var principal = CreatePrincipal("principal-1");
        principal.IsBlocked = true;
        await _store.AddAsync(principal, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var loaded = await _store.GetOrDefaultAsync("principal-1", CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.True(loaded.IsBlocked);
    }

    [Fact]
    public async Task UpdateAsync_WhenBlocked_PersistsTheBlockState()
    {
        await _store.AddAsync(CreatePrincipal("principal-1"), CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var loaded = await _store.GetOrDefaultAsync("principal-1", CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.False(loaded.IsBlocked);
        loaded.IsBlocked = true;
        await _store.UpdateAsync(loaded, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var reloaded = await _store.GetOrDefaultAsync("principal-1", CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.True(reloaded.IsBlocked);
    }

    #region RemoveAsync Tests

    [Fact]
    public async Task RemoveAsync_RemovesTheRoleAssignmentsOfThePrincipalInEveryTenant()
    {
        var idGenerator = _provider.GetRequiredService<IIdGenerator<long>>();
        _dbContext.SeedTenant(idGenerator, "tenant-2");
        await _store.AddAsync(CreatePrincipal("principal-1"), CancellationToken.None);
        await _store.AddAsync(CreatePrincipal("principal-2"), CancellationToken.None);
        var roleAssignmentStore = new RoleAssignmentStore(
            Mock.Of<IStoreProvider>(),
            idGenerator,
            _dbContext
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
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        await _store.RemoveAsync("principal-1", CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var remaining = await _dbContext
            .RoleAssignments.IgnoreQueryFilters()
            .Select(entity => entity.AssignmentId)
            .OrderBy(assignmentId => assignmentId)
            .ToListAsync();
        Assert.Equal(["assign-3"], remaining);
        Assert.Null(await _store.GetOrDefaultAsync("principal-1", CancellationToken.None));
    }

    [Fact]
    public async Task RemoveAsync_RemovesTheConsentsAndGrantsOfThePrincipal()
    {
        var idGenerator = _provider.GetRequiredService<IIdGenerator<long>>();
        const string clientId = "client-1";
        await _store.AddAsync(CreatePrincipal("principal-1"), CancellationToken.None);
        await _store.AddAsync(CreatePrincipal("principal-2"), CancellationToken.None);
        var clientStore = new ClientStore(Mock.Of<IStoreProvider>(), idGenerator, _dbContext);
        await clientStore.AddAsync(CreateClient(clientId), CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        var consentStore = new ConsentStore(Mock.Of<IStoreProvider>(), idGenerator, _dbContext);
        await consentStore.SaveAsync(
            CreateConsent(clientId, "principal-1"),
            CancellationToken.None
        );
        await consentStore.SaveAsync(
            CreateConsent(clientId, "principal-2"),
            CancellationToken.None
        );
        var grantStore = new GrantStore(Mock.Of<IStoreProvider>(), idGenerator, _dbContext);
        await grantStore.AddAsync(CreateGrant(clientId, "principal-1"), CancellationToken.None);
        await grantStore.AddAsync(CreateGrant(clientId, "principal-2"), CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        await _store.RemoveAsync("principal-1", CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var remainingConsents = await _dbContext
            .Consents.IgnoreQueryFilters()
            .Select(entity => entity.SubjectId)
            .ToListAsync();
        var remainingGrants = await _dbContext
            .Grants.IgnoreQueryFilters()
            .Select(entity => entity.SubjectId)
            .ToListAsync();
        Assert.Equal(["principal-2"], remainingConsents);
        Assert.Equal(["principal-2"], remainingGrants);
    }

    #endregion
}
