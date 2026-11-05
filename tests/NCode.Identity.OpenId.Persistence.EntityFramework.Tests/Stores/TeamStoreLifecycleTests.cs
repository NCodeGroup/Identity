// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using IdGen;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

public sealed class TeamStoreLifecycleTests : IDisposable
{
    private const string TeamId = "team-1";

    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;
    private readonly IIdGenerator<long> _idGenerator;
    private readonly OpenIdDbContext _dbContext;
    private readonly TeamStore _store;

    public TeamStoreLifecycleTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options => options.UseSqlite(_connection));

        _provider = serviceCollection.BuildServiceProvider();
        _dbContext = _provider.GetRequiredService<OpenIdDbContext>();
        _dbContext.Database.EnsureCreated();
        _idGenerator = _provider.GetRequiredService<IIdGenerator<long>>();
        _store = new TeamStore(Mock.Of<IStoreProvider>(), _idGenerator, _dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _provider.Dispose();
        _connection.Dispose();
    }

    private static PersistedTeam CreateTeam() =>
        new()
        {
            TeamId = TeamId,
            ConcurrencyToken = string.Empty,
            IsDisabled = false,
            DisplayName = "Team One",
            Settings = new PersistedTeamSettings
            {
                TeamId = TeamId,
                ConcurrencyToken = string.Empty,
                Value = JsonSerializer.SerializeToElement(new Dictionary<string, object>()),
            },
        };

    private async Task SaveAndClearAsync()
    {
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    private async Task SeedTeamAsync()
    {
        await _store.AddAsync(CreateTeam(), CancellationToken.None);
        await SaveAndClearAsync();
    }

    private async Task<PersistedTeam> GetTeamAsync()
    {
        var team = await _store.GetOrDefaultAsync(TeamId, CancellationToken.None);
        Assert.NotNull(team);
        return team;
    }

    private static PersistedTeamSettings NewSettings(PersistedTeam team, string key) =>
        new()
        {
            TeamId = TeamId,
            ConcurrencyToken = team.Settings.ConcurrencyToken,
            Value = JsonSerializer.SerializeToElement(new Dictionary<string, object> { [key] = 1 }),
        };

    private async Task SeedTenantInTeamAsync()
    {
        var team = _dbContext.SeedTeam(_idGenerator, TeamId);
        _dbContext
            .Set<TenantEntity>()
            .Add(
                new TenantEntity
                {
                    Id = _idGenerator.CreateId(),
                    TeamId = team.Id,
                    Team = team,
                    TenantId = "tenant-in-team",
                    NormalizedTenantId = "tenant-in-team",
                    Issuer = null,
                    NormalizedIssuer = null,
                    NormalizedIssuerHost = null,
                    MtlsEndpointAliasAuthority = null,
                    NormalizedMtlsEndpointAliasHost = null,
                    ConcurrencyToken = string.Empty,
                    SettingsConcurrencyToken = string.Empty,
                    SecretsConcurrencyToken = string.Empty,
                    IsDisabled = false,
                    DisplayName = "Tenant",
                    SettingsJson = JsonSerializer.SerializeToElement(
                        new Dictionary<string, object>()
                    ),
                    Secrets = [],
                }
            );
        await SaveAndClearAsync();
    }

    [Fact]
    public async Task AddAsync_AssignsRowAndSettingsConcurrencyTokens()
    {
        var team = CreateTeam();

        await _store.AddAsync(team, CancellationToken.None);

        Assert.False(string.IsNullOrEmpty(team.ConcurrencyToken));
        Assert.False(string.IsNullOrEmpty(team.Settings.ConcurrencyToken));
        Assert.NotEqual(team.ConcurrencyToken, team.Settings.ConcurrencyToken);
    }

    [Fact]
    public async Task GetOrDefaultAsync_RoundTripsDisplayNameAndIsDisabled()
    {
        var team = CreateTeam();
        team.IsDisabled = true;
        await _store.AddAsync(team, CancellationToken.None);
        await SaveAndClearAsync();

        var loaded = await GetTeamAsync();

        Assert.Equal("Team One", loaded.DisplayName);
        Assert.True(loaded.IsDisabled);
    }

    [Fact]
    public async Task UpdateAsync_WhenTokenMismatch_Throws()
    {
        await SeedTeamAsync();
        var team = await GetTeamAsync();
        team.ConcurrencyToken = "stale";

        await Assert.ThrowsAsync<StoreConcurrencyException>(async () =>
            await _store.UpdateAsync(team, CancellationToken.None)
        );
    }

    [Fact]
    public async Task UpdateAsync_PersistsIsDisabledAndDisplayName()
    {
        await SeedTeamAsync();
        var team = await GetTeamAsync();
        team.IsDisabled = true;
        team.DisplayName = "Renamed Team";

        await _store.UpdateAsync(team, CancellationToken.None);
        await SaveAndClearAsync();

        var after = await GetTeamAsync();
        Assert.True(after.IsDisabled);
        Assert.Equal("Renamed Team", after.DisplayName);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenTokenMismatch_Throws()
    {
        await SeedTeamAsync();
        var team = await GetTeamAsync();
        var settings = NewSettings(team, "a");
        settings.ConcurrencyToken = "stale";

        await Assert.ThrowsAsync<StoreConcurrencyException>(async () =>
            await _store.UpdateSettingsAsync(settings, CancellationToken.None)
        );
    }

    [Fact]
    public async Task UpdateReconciledSettingsAsync_WhenTheRowChanged_Throws()
    {
        await SeedTeamAsync();
        var team = await GetTeamAsync();
        team.ConcurrencyToken = "stale";
        team.ReconciledSettingsFingerprint = "fingerprint";

        await Assert.ThrowsAsync<StoreConcurrencyException>(async () =>
            await _store.UpdateReconciledSettingsAsync(team, CancellationToken.None)
        );
    }

    [Fact]
    public async Task UpdateSettingsAsync_ClearsTheReconciledSettingsFingerprint()
    {
        await SeedTeamAsync();
        var team = await GetTeamAsync();
        team.ReconciledSettingsFingerprint = "fingerprint";
        team.ReconciledSettings = JsonSerializer.SerializeToElement(
            new Dictionary<string, object>()
        );
        await _store.UpdateReconciledSettingsAsync(team, CancellationToken.None);
        await SaveAndClearAsync();

        var reloaded = await GetTeamAsync();
        await _store.UpdateSettingsAsync(NewSettings(reloaded, "a"), CancellationToken.None);
        await SaveAndClearAsync();

        var after = await GetTeamAsync();
        Assert.Null(after.ReconciledSettingsFingerprint);
        Assert.NotNull(after.ReconciledSettings);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenAnEarlierChangeWasNotReconciled_ClearsTheReconciledSettings()
    {
        await SeedTeamAsync();
        var team = await GetTeamAsync();
        team.ReconciledSettingsFingerprint = "fingerprint";
        team.ReconciledSettings = JsonSerializer.SerializeToElement(
            new Dictionary<string, object>()
        );
        await _store.UpdateReconciledSettingsAsync(team, CancellationToken.None);
        await SaveAndClearAsync();

        var first = await GetTeamAsync();
        await _store.UpdateSettingsAsync(NewSettings(first, "a"), CancellationToken.None);
        await SaveAndClearAsync();

        var second = await GetTeamAsync();
        await _store.UpdateSettingsAsync(NewSettings(second, "b"), CancellationToken.None);
        await SaveAndClearAsync();

        var after = await GetTeamAsync();
        Assert.Null(after.ReconciledSettingsFingerprint);
        Assert.Null(after.ReconciledSettings);
    }

    [Fact]
    public async Task UpdateReconciledSettingsAsync_RecordsTheFingerprintAndTheSettings()
    {
        await SeedTeamAsync();
        var team = await GetTeamAsync();
        team.ReconciledSettingsFingerprint = "fingerprint";
        team.ReconciledSettings = JsonSerializer.SerializeToElement(
            new Dictionary<string, object> { ["k"] = 1 }
        );

        await _store.UpdateReconciledSettingsAsync(team, CancellationToken.None);
        await SaveAndClearAsync();

        var after = await GetTeamAsync();
        Assert.Equal("fingerprint", after.ReconciledSettingsFingerprint);
        Assert.NotNull(after.ReconciledSettings);
        Assert.Equal(1, after.ReconciledSettings.Value.GetProperty("k").GetInt32());
    }

    [Fact]
    public async Task RemoveAsync_WhenTheTeamHasTenants_Throws()
    {
        await SeedTenantInTeamAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _store.RemoveAsync(TeamId, CancellationToken.None)
        );

        Assert.Contains("still has one or more tenants", exception.Message);
    }

    [Fact]
    public async Task RemoveAsync_RemovesTheTeamNodeAssignmentsAndRetiresTheId()
    {
        await SeedTeamAsync();
        var tenant = _dbContext.SeedTenant(_idGenerator, "tenant-1");
        RoleAssignmentEntity CreateAssignment(string assignmentId, string resourceType) =>
            new()
            {
                Id = _idGenerator.CreateId(),
                TenantId = tenant.Id,
                Tenant = tenant,
                NormalizedTenantId = tenant.NormalizedTenantId,
                AssignmentId = assignmentId,
                NormalizedAssignmentId = assignmentId,
                PrincipalId = "principal-1",
                NormalizedPrincipalId = "principal-1",
                RoleName = "owner",
                NormalizedRoleName = "owner",
                ResourceType = resourceType,
                ResourceId = TeamId,
                NormalizedResourceId = TeamId,
                ConcurrencyToken = string.Empty,
            };
        _dbContext
            .Set<RoleAssignmentEntity>()
            .AddRange(
                CreateAssignment("assignment-1", OpenIdResourceTypes.Team),
                CreateAssignment("assignment-2", OpenIdResourceTypes.Tenant)
            );
        await SaveAndClearAsync();

        var removed = await _store.RemoveAsync(TeamId, CancellationToken.None);
        await SaveAndClearAsync();

        Assert.True(removed);
        Assert.Null(await _store.GetOrDefaultAsync(TeamId, CancellationToken.None));
        Assert.True(await _store.IsTeamIdRetiredAsync(TeamId, CancellationToken.None));
        var remaining = await _dbContext.Set<RoleAssignmentEntity>().ToListAsync();
        var survivor = Assert.Single(remaining);
        Assert.Equal(OpenIdResourceTypes.Tenant, survivor.ResourceType);
    }

    [Fact]
    public async Task RemoveAsync_WhenMissing_ReturnsFalse()
    {
        Assert.False(await _store.RemoveAsync(TeamId, CancellationToken.None));
    }

    [Fact]
    public async Task HasDependentsAsync_WhenATenantExists_ReturnsTrue()
    {
        await SeedTenantInTeamAsync();

        Assert.True(await _store.HasDependentsAsync(TeamId, CancellationToken.None));
    }

    [Fact]
    public async Task GetOrDefaultAsync_WithADifferentCasing_FindsTheTeam()
    {
        await SeedTeamAsync();

        var team = await _store.GetOrDefaultAsync("TEAM-1", CancellationToken.None);

        Assert.NotNull(team);
        Assert.Equal(TeamId, team.TeamId);
    }

    [Fact]
    public async Task GetSettingsAsync_WithADifferentCasing_FindsTheTeam()
    {
        await SeedTeamAsync();

        var settings = await _store.GetSettingsAsync("TEAM-1", CancellationToken.None);

        Assert.Equal(TeamId, settings.TeamId);
    }

    [Fact]
    public async Task HasDependentsAsync_WithADifferentCasing_FindsTheTeam()
    {
        await SeedTenantInTeamAsync();

        Assert.True(await _store.HasDependentsAsync("TEAM-1", CancellationToken.None));
    }

    [Fact]
    public async Task RemoveAsync_WithADifferentCasing_RemovesTheTeam()
    {
        await SeedTeamAsync();

        var removed = await _store.RemoveAsync("TEAM-1", CancellationToken.None);
        await SaveAndClearAsync();

        Assert.True(removed);
        Assert.Null(await _store.GetOrDefaultAsync(TeamId, CancellationToken.None));
    }

    [Fact]
    public async Task IsTeamIdRetiredAsync_WithADifferentCasing_ReturnsTrueAfterRemoval()
    {
        await SeedTeamAsync();
        await _store.RemoveAsync(TeamId, CancellationToken.None);
        await SaveAndClearAsync();

        Assert.True(await _store.IsTeamIdRetiredAsync("TEAM-1", CancellationToken.None));
    }

    [Fact]
    public async Task IsTeamIdRetiredAsync_AfterRemoval_ReturnsTrue()
    {
        await SeedTeamAsync();
        Assert.False(await _store.IsTeamIdRetiredAsync(TeamId, CancellationToken.None));

        await _store.RemoveAsync(TeamId, CancellationToken.None);
        await SaveAndClearAsync();

        Assert.True(await _store.IsTeamIdRetiredAsync(TeamId, CancellationToken.None));
    }
}
