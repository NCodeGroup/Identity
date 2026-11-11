// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using IdGen;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

public sealed class TeamStoreLifecycleTests : IDisposable
{
    private const string TeamId = "team-1";

    private SqliteConnection Connection { get; }
    private ServiceProvider Provider { get; }
    private IIdGenerator<long> IdGenerator { get; }
    private OpenIdDbContext DbContext { get; }
    private TeamStore Store { get; }

    public TeamStoreLifecycleTests()
    {
        Connection = new SqliteConnection("DataSource=:memory:");
        Connection.Open();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options => options.UseSqlite(Connection));

        Provider = serviceCollection.BuildServiceProvider();
        DbContext = Provider.GetRequiredService<OpenIdDbContext>();
        DbContext.Database.EnsureCreated();
        IdGenerator = Provider.GetRequiredService<IIdGenerator<long>>();
        Store = new TeamStore(Mock.Of<IStoreProvider>(), IdGenerator, DbContext);
    }

    public void Dispose()
    {
        DbContext.Dispose();
        Provider.Dispose();
        Connection.Dispose();
    }

    private static PersistedTeam CreateTeam(string teamId = TeamId) =>
        new PersistedTeamBuilder { TeamId = teamId, DisplayName = "Team One" }.Build();

    private async ValueTask SaveAndClearAsync(CancellationToken cancellationToken)
    {
        await DbContext.SaveChangesAsync(cancellationToken);
        DbContext.ChangeTracker.Clear();
    }

    private async ValueTask SeedTeamAsync(CancellationToken cancellationToken)
    {
        await Store.AddAsync(CreateTeam(), cancellationToken);
        await SaveAndClearAsync(cancellationToken);
    }

    private async ValueTask<PersistedTeam> GetTeamAsync(CancellationToken cancellationToken)
    {
        var team = await Store.GetOrDefaultAsync(TeamId, cancellationToken);
        Assert.NotNull(team);
        return team;
    }

    private static PersistedTeamSettings NewSettings(PersistedTeam team, string key) =>
        new PersistedTeamBuilder
        {
            TeamId = TeamId,
            SettingsConcurrencyToken = team.Settings.ConcurrencyToken,
            Settings = JsonSerializer.SerializeToElement(
                new Dictionary<string, object> { [key] = 1 }
            ),
        }.BuildSettings();

    private async ValueTask SeedTenantInTeamAsync(CancellationToken cancellationToken)
    {
        var team = DbContext.SeedTeam(IdGenerator, TeamId);
        DbContext
            .Set<TenantEntity>()
            .Add(
                new TenantEntity
                {
                    Id = IdGenerator.CreateId(),
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
        await SaveAndClearAsync(cancellationToken);
    }

    [Fact]
    public async Task AddAsync_AssignsRowAndSettingsConcurrencyTokens()
    {
        var team = CreateTeam();

        await Store.AddAsync(team, CancellationToken.None);

        Assert.False(string.IsNullOrEmpty(team.ConcurrencyToken));
        Assert.False(string.IsNullOrEmpty(team.Settings.ConcurrencyToken));
        Assert.NotEqual(team.ConcurrencyToken, team.Settings.ConcurrencyToken);
    }

    [Fact]
    public async Task GetOrDefaultAsync_RoundTripsDisplayNameAndIsDisabled()
    {
        var team = CreateTeam();
        team.IsDisabled = true;
        await Store.AddAsync(team, CancellationToken.None);
        await SaveAndClearAsync(CancellationToken.None);

        var loaded = await GetTeamAsync(CancellationToken.None);

        Assert.Equal("Team One", loaded.DisplayName);
        Assert.True(loaded.IsDisabled);
    }

    [Fact]
    public async Task GetConcurrencyTokensAsync_ReturnsTheRowTokenOfEachExistingTeam()
    {
        await SeedTeamAsync(CancellationToken.None);
        var team = await GetTeamAsync(CancellationToken.None);

        var tokens = await Store.GetConcurrencyTokensAsync(
            [TeamId.ToUpperInvariant(), "missing-team"],
            CancellationToken.None
        );

        Assert.Single(tokens);
        Assert.Equal(team.ConcurrencyToken, tokens[TeamId]);
    }

    [Fact]
    public async Task GetManyAsync_ReturnsTheExistingTeamsOrderedAndSkipsTheMissing()
    {
        var first = CreateTeam();
        await Store.AddAsync(first, CancellationToken.None);
        var second = CreateTeam("team-2");
        await Store.AddAsync(second, CancellationToken.None);
        await SaveAndClearAsync(CancellationToken.None);

        var teams = await Store.GetManyAsync(
            ["TEAM-2", TeamId.ToUpperInvariant(), "missing-team"],
            CancellationToken.None
        );

        Assert.Equal([TeamId, "team-2"], teams.Select(team => team.TeamId));
        Assert.All(teams, team => Assert.Equal("Team One", team.DisplayName));
    }

    [Fact]
    public async Task UpdateAsync_WhenTokenMismatch_Throws()
    {
        await SeedTeamAsync(CancellationToken.None);
        var team = await GetTeamAsync(CancellationToken.None);
        team.ConcurrencyToken = "stale";

        await Assert.ThrowsAsync<StoreConcurrencyException>(async () =>
            await Store.UpdateAsync(team, CancellationToken.None)
        );
    }

    [Fact]
    public async Task UpdateAsync_PersistsIsDisabledAndDisplayName()
    {
        await SeedTeamAsync(CancellationToken.None);
        var team = await GetTeamAsync(CancellationToken.None);
        team.IsDisabled = true;
        team.DisplayName = "Renamed Team";

        await Store.UpdateAsync(team, CancellationToken.None);
        await SaveAndClearAsync(CancellationToken.None);

        var after = await GetTeamAsync(CancellationToken.None);
        Assert.True(after.IsDisabled);
        Assert.Equal("Renamed Team", after.DisplayName);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenTokenMismatch_Throws()
    {
        await SeedTeamAsync(CancellationToken.None);
        var team = await GetTeamAsync(CancellationToken.None);
        var settings = NewSettings(team, "a");
        settings.ConcurrencyToken = "stale";

        await Assert.ThrowsAsync<StoreConcurrencyException>(async () =>
            await Store.UpdateSettingsAsync(settings, CancellationToken.None)
        );
    }

    [Fact]
    public async Task UpdateReconciledSettingsAsync_WhenTheRowChanged_Throws()
    {
        await SeedTeamAsync(CancellationToken.None);
        var team = await GetTeamAsync(CancellationToken.None);
        team.ConcurrencyToken = "stale";
        team.ReconciledSettingsFingerprint = "fingerprint";

        await Assert.ThrowsAsync<StoreConcurrencyException>(async () =>
            await Store.UpdateReconciledSettingsAsync(team, CancellationToken.None)
        );
    }

    [Fact]
    public async Task UpdateSettingsAsync_ClearsTheReconciledSettingsFingerprint()
    {
        await SeedTeamAsync(CancellationToken.None);
        var team = await GetTeamAsync(CancellationToken.None);
        team.ReconciledSettingsFingerprint = "fingerprint";
        team.ReconciledSettings = JsonSerializer.SerializeToElement(
            new Dictionary<string, object>()
        );
        await Store.UpdateReconciledSettingsAsync(team, CancellationToken.None);
        await SaveAndClearAsync(CancellationToken.None);

        var reloaded = await GetTeamAsync(CancellationToken.None);
        await Store.UpdateSettingsAsync(NewSettings(reloaded, "a"), CancellationToken.None);
        await SaveAndClearAsync(CancellationToken.None);

        var after = await GetTeamAsync(CancellationToken.None);
        Assert.Null(after.ReconciledSettingsFingerprint);
        Assert.NotNull(after.ReconciledSettings);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenAnEarlierChangeWasNotReconciled_ClearsTheReconciledSettings()
    {
        await SeedTeamAsync(CancellationToken.None);
        var team = await GetTeamAsync(CancellationToken.None);
        team.ReconciledSettingsFingerprint = "fingerprint";
        team.ReconciledSettings = JsonSerializer.SerializeToElement(
            new Dictionary<string, object>()
        );
        await Store.UpdateReconciledSettingsAsync(team, CancellationToken.None);
        await SaveAndClearAsync(CancellationToken.None);

        var first = await GetTeamAsync(CancellationToken.None);
        await Store.UpdateSettingsAsync(NewSettings(first, "a"), CancellationToken.None);
        await SaveAndClearAsync(CancellationToken.None);

        var second = await GetTeamAsync(CancellationToken.None);
        await Store.UpdateSettingsAsync(NewSettings(second, "b"), CancellationToken.None);
        await SaveAndClearAsync(CancellationToken.None);

        var after = await GetTeamAsync(CancellationToken.None);
        Assert.Null(after.ReconciledSettingsFingerprint);
        Assert.Null(after.ReconciledSettings);
    }

    [Fact]
    public async Task UpdateReconciledSettingsAsync_RecordsTheFingerprintAndTheSettings()
    {
        await SeedTeamAsync(CancellationToken.None);
        var team = await GetTeamAsync(CancellationToken.None);
        team.ReconciledSettingsFingerprint = "fingerprint";
        team.ReconciledSettings = JsonSerializer.SerializeToElement(
            new Dictionary<string, object> { ["k"] = 1 }
        );

        await Store.UpdateReconciledSettingsAsync(team, CancellationToken.None);
        await SaveAndClearAsync(CancellationToken.None);

        var after = await GetTeamAsync(CancellationToken.None);
        Assert.Equal("fingerprint", after.ReconciledSettingsFingerprint);
        Assert.NotNull(after.ReconciledSettings);
        Assert.Equal(1, after.ReconciledSettings.Value.GetProperty("k").GetInt32());
    }

    [Fact]
    public async Task RemoveAsync_WhenTheTeamHasTenants_Throws()
    {
        await SeedTenantInTeamAsync(CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Store.RemoveAsync(TeamId, CancellationToken.None)
        );

        Assert.Contains(
            "still has one or more tenants, teams or machine clients",
            exception.Message
        );
    }

    [Fact]
    public async Task RemoveAsync_RemovesTheTeamNodeAssignmentsAndRetiresTheId()
    {
        await SeedTeamAsync(CancellationToken.None);
        var tenant = DbContext.SeedTenant(IdGenerator, "tenant-1");
        RoleAssignmentEntity CreateAssignment(string assignmentId, string resourceType) =>
            new()
            {
                Id = IdGenerator.CreateId(),
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
        DbContext
            .Set<RoleAssignmentEntity>()
            .AddRange(
                CreateAssignment("assignment-1", OpenIdResourceTypes.Team),
                CreateAssignment("assignment-2", OpenIdResourceTypes.Tenant)
            );
        await SaveAndClearAsync(CancellationToken.None);

        var removed = await Store.RemoveAsync(TeamId, CancellationToken.None);
        await SaveAndClearAsync(CancellationToken.None);

        Assert.True(removed);
        Assert.Null(await Store.GetOrDefaultAsync(TeamId, CancellationToken.None));
        Assert.True(await Store.IsTeamIdRetiredAsync(TeamId, CancellationToken.None));
        var remaining = await DbContext.Set<RoleAssignmentEntity>().ToListAsync();
        var survivor = Assert.Single(remaining);
        Assert.Equal(OpenIdResourceTypes.Tenant, survivor.ResourceType);
    }

    [Fact]
    public async Task RemoveAsync_WhenMissing_ReturnsFalse()
    {
        Assert.False(await Store.RemoveAsync(TeamId, CancellationToken.None));
    }

    [Fact]
    public async Task HasDependentsAsync_WhenATenantExists_ReturnsTrue()
    {
        await SeedTenantInTeamAsync(CancellationToken.None);

        Assert.True(await Store.HasDependentsAsync(TeamId, CancellationToken.None));
    }

    [Fact]
    public async Task HasDependentsAsync_WhenTheTeamOwnsAMachineClient_ReturnsTrue()
    {
        var team = DbContext.SeedTeam(IdGenerator, TeamId);
        var tenant = DbContext.SeedTenant(IdGenerator, "tenant-1");
        DbContext
            .Set<ClientEntity>()
            .Add(
                new ClientEntity
                {
                    Id = IdGenerator.CreateId(),
                    TenantId = tenant.Id,
                    NormalizedTenantId = tenant.NormalizedTenantId,
                    ClientId = "machine-client",
                    NormalizedClientId = "machine-client",
                    PrincipalId = "principal-1",
                    NormalizedPrincipalId = "principal-1",
                    OwningNodeType = OpenIdResourceTypes.Team,
                    OwningNodeId = team.TeamId,
                    NormalizedOwningNodeId = team.NormalizedTeamId,
                    ConcurrencyToken = string.Empty,
                    SettingsConcurrencyToken = string.Empty,
                    SecretsConcurrencyToken = string.Empty,
                    IsDisabled = false,
                    SettingsJson = JsonSerializer.SerializeToElement(
                        new Dictionary<string, object>()
                    ),
                    ResourceServerId = null,
                    Tenant = tenant,
                    ResourceServer = null,
                    Secrets = [],
                }
            );
        await SaveAndClearAsync(CancellationToken.None);

        Assert.True(await Store.HasDependentsAsync(TeamId, CancellationToken.None));
    }

    [Fact]
    public async Task GetOrDefaultAsync_WithADifferentCasing_FindsTheTeam()
    {
        await SeedTeamAsync(CancellationToken.None);

        var team = await Store.GetOrDefaultAsync("TEAM-1", CancellationToken.None);

        Assert.NotNull(team);
        Assert.Equal(TeamId, team.TeamId);
    }

    [Fact]
    public async Task GetSettingsAsync_WithADifferentCasing_FindsTheTeam()
    {
        await SeedTeamAsync(CancellationToken.None);

        var settings = await Store.GetSettingsAsync("TEAM-1", CancellationToken.None);

        Assert.Equal(TeamId, settings.TeamId);
    }

    [Fact]
    public async Task HasDependentsAsync_WithADifferentCasing_FindsTheTeam()
    {
        await SeedTenantInTeamAsync(CancellationToken.None);

        Assert.True(await Store.HasDependentsAsync("TEAM-1", CancellationToken.None));
    }

    [Fact]
    public async Task RemoveAsync_WithADifferentCasing_RemovesTheTeam()
    {
        await SeedTeamAsync(CancellationToken.None);

        var removed = await Store.RemoveAsync("TEAM-1", CancellationToken.None);
        await SaveAndClearAsync(CancellationToken.None);

        Assert.True(removed);
        Assert.Null(await Store.GetOrDefaultAsync(TeamId, CancellationToken.None));
    }

    [Fact]
    public async Task IsTeamIdRetiredAsync_WithADifferentCasing_ReturnsTrueAfterRemoval()
    {
        await SeedTeamAsync(CancellationToken.None);
        await Store.RemoveAsync(TeamId, CancellationToken.None);
        await SaveAndClearAsync(CancellationToken.None);

        Assert.True(await Store.IsTeamIdRetiredAsync("TEAM-1", CancellationToken.None));
    }

    [Fact]
    public async Task IsTeamIdRetiredAsync_AfterRemoval_ReturnsTrue()
    {
        await SeedTeamAsync(CancellationToken.None);
        Assert.False(await Store.IsTeamIdRetiredAsync(TeamId, CancellationToken.None));

        await Store.RemoveAsync(TeamId, CancellationToken.None);
        await SaveAndClearAsync(CancellationToken.None);

        Assert.True(await Store.IsTeamIdRetiredAsync(TeamId, CancellationToken.None));
    }
}
