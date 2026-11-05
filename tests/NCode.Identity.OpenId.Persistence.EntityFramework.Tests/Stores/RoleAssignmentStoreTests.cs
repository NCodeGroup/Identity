// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using IdGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

public sealed class RoleAssignmentStoreTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string Subject = "subject-1";

    private readonly SwitchableAmbientTenantAccessor _ambient = new();
    private readonly ServiceProvider _provider;
    private readonly OpenIdDbContext _dbContext;
    private readonly TenantStore _tenantStore;
    private readonly RoleAssignmentStore _store;

    public RoleAssignmentStoreTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<IAmbientTenantAccessor>(_ambient);
        serviceCollection.AddTestOpenIdDbContext(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        );

        _provider = serviceCollection.BuildServiceProvider();
        _dbContext = _provider.GetRequiredService<OpenIdDbContext>();
        var idGenerator = _provider.GetRequiredService<IIdGenerator<long>>();
        _tenantStore = new TenantStore(Mock.Of<IStoreProvider>(), idGenerator, _dbContext);
        _store = new RoleAssignmentStore(Mock.Of<IStoreProvider>(), idGenerator, _dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _provider.Dispose();
    }

    private static JsonElement EmptyObject() =>
        JsonSerializer.SerializeToElement(new Dictionary<string, object>());

    private static PersistedRoleAssignment CreateAssignment(
        string assignmentId,
        string principalId,
        string roleName,
        string resourceType,
        string resourceId
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
        await _tenantStore.AddAsync(
            new PersistedTenant
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
                    Value = EmptyObject(),
                },
                Secrets = new PersistedTenantSecrets
                {
                    TenantId = TenantId,
                    ConcurrencyToken = string.Empty,
                    Value = [],
                },
            },
            CancellationToken.None
        );

        // The tenant must be persisted before its dependents are added (an in-memory query does not observe
        // unsaved inserts).
        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task AddAsync_WhenPersisted_RoundTripsByAssignmentId()
    {
        await SeedTenantAsync();

        await _store.AddAsync(
            CreateAssignment("assign-1", Subject, "Owner", "client", "client-1"),
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();

        var loaded = await _store.GetOrDefaultAsync("assign-1", CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(TenantId, loaded.TenantId);
        Assert.Equal(Subject, loaded.PrincipalId);
        Assert.Equal("Owner", loaded.RoleName);
        Assert.Equal("client", loaded.ResourceType);
        Assert.Equal("client-1", loaded.ResourceId);
        Assert.NotEmpty(loaded.ConcurrencyToken);
    }

    [Fact]
    public async Task GetByResourceAsync_WhenNodeHasOwners_ReturnsOnlyThatNode()
    {
        await SeedTenantAsync();

        await _store.AddAsync(
            CreateAssignment("assign-1", "owner-a", "Owner", "client", "client-1"),
            CancellationToken.None
        );
        await _store.AddAsync(
            CreateAssignment("assign-2", "owner-b", "Owner", "client", "client-1"),
            CancellationToken.None
        );
        await _store.AddAsync(
            CreateAssignment("assign-3", "owner-c", "Owner", "client", "client-2"),
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();

        var atNode = await _store.GetByResourceAsync("client", "client-1", CancellationToken.None);

        Assert.Equal(2, atNode.Count);
        Assert.All(atNode, assignment => Assert.Equal("client-1", assignment.ResourceId));
    }

    [Fact]
    public async Task GetByPrincipalAsync_ReturnsEveryAssignmentForThePrincipal()
    {
        await SeedTenantAsync();

        await _store.AddAsync(
            CreateAssignment("assign-1", Subject, "Owner", "client", "client-1"),
            CancellationToken.None
        );
        await _store.AddAsync(
            CreateAssignment("assign-2", Subject, "TenantAdmin", "tenant", TenantId),
            CancellationToken.None
        );
        await _store.AddAsync(
            CreateAssignment("assign-3", "other", "Owner", "client", "client-3"),
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();

        var forPrincipal = await _store.GetByPrincipalAsync(Subject, CancellationToken.None);

        Assert.Equal(2, forPrincipal.Count);
        Assert.All(forPrincipal, assignment => Assert.Equal(Subject, assignment.PrincipalId));
    }

    // See ADR-1411
    [Fact]
    public async Task GetByPrincipalAsync_ReadsAssignmentsOfEveryTenant()
    {
        await SeedTenantAsync();
        _dbContext.SeedTenant(_provider.GetRequiredService<IIdGenerator<long>>(), "tenant-2");

        await _store.AddAsync(
            CreateAssignment("assign-1", Subject, "Owner", "client", "client-1"),
            CancellationToken.None
        );
        await _store.AddAsync(
            new PersistedRoleAssignment
            {
                TenantId = "tenant-2",
                AssignmentId = "assign-2",
                PrincipalId = Subject,
                RoleName = "TeamAdmin",
                ResourceType = "team",
                ResourceId = "platform",
                ConcurrencyToken = string.Empty,
            },
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();

        using var scope = _ambient.BeginScope("tenant-2");
        var forPrincipal = await _store.GetByPrincipalAsync(Subject, CancellationToken.None);

        Assert.Equal(2, forPrincipal.Count);
        Assert.Contains(forPrincipal, assignment => assignment.TenantId == TenantId);
        Assert.Contains(forPrincipal, assignment => assignment.TenantId == "tenant-2");
    }

    [Fact]
    public async Task RemoveAsync_WhenAssignmentExists_RemovesIt()
    {
        await SeedTenantAsync();

        await _store.AddAsync(
            CreateAssignment("assign-1", Subject, "Owner", "client", "client-1"),
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();

        await _store.RemoveAsync("assign-1", CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        var loaded = await _store.GetOrDefaultAsync("assign-1", CancellationToken.None);
        Assert.Null(loaded);
    }
}

/// <summary>
/// An ambient tenant accessor whose scope a test begins and ends, so the tenant-scoping query filter applies.
/// </summary>
internal sealed class SwitchableAmbientTenantAccessor : IAmbientTenantAccessor
{
    public string? TenantId { get; private set; }

    public bool IsScoped => TenantId is not null;

    public IDisposable BeginScope(string tenantId)
    {
        TenantId = tenantId;
        return new Scope(this);
    }

    private sealed class Scope(SwitchableAmbientTenantAccessor accessor) : IDisposable
    {
        public void Dispose() => accessor.TenantId = null;
    }
}
