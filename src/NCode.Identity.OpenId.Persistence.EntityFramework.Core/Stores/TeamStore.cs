// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Linq.Expressions;
using IdGen;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.EntityFramework.Core.Entities;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Core.Stores;

/// <summary>
/// Provides a default implementation of <see cref="ITeamStore"/> that uses Entity Framework Core for persistence.
/// A team is removed only when it holds no tenant and no team, and a removed team's identifier is never reissued.
/// </summary>
// See ADR-1411
// See ADR-1426.
internal class TeamStore(
    IStoreProvider storeProvider,
    IIdGenerator<long> idGenerator,
    OpenIdDbContext openIdDbContext
) : CoreBaseStoreWithResourceId<PersistedTeam, TeamEntity>, ITeamStore
{
    /// <inheritdoc />
    protected override IStoreProvider StoreProvider { get; } = storeProvider;

    /// <inheritdoc />
    protected override IIdGenerator<long> IdGenerator { get; } = idGenerator;

    /// <inheritdoc />
    protected override OpenIdDbContext DbContext { get; } = openIdDbContext;

    /// <summary>
    /// Maps a <see cref="TeamEntity"/> to a <see cref="PersistedTeamSettings"/> instance.
    /// </summary>
    /// <param name="teamEntity">The <see cref="TeamEntity"/> instance to map.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the newly mapped <see cref="PersistedTeamSettings"/> instance.</returns>
    protected internal virtual ValueTask<PersistedTeamSettings> MapSettingsAsync(
        TeamEntity teamEntity,
        CancellationToken cancellationToken
    )
    {
        return ValueTask.FromResult(
            new PersistedTeamSettings
            {
                TeamId = teamEntity.TeamId,
                ConcurrencyToken = teamEntity.SettingsConcurrencyToken,
                Value = teamEntity.SettingsJson,
            }
        );
    }

    /// <inheritdoc />
    protected override async ValueTask<PersistedTeam> MapFromEntityAsync(
        TeamEntity entity,
        CancellationToken cancellationToken
    )
    {
        var teamIds = TeamPaths.Parse(entity.Path);

        return new PersistedTeam
        {
            TeamId = entity.TeamId,
            ParentTeamId = teamIds.Count > 1 ? teamIds[^2] : null,
            AncestorTeamIds = [.. teamIds.Take(teamIds.Count - 1)],
            ConcurrencyToken = entity.ConcurrencyToken,
            IsDisabled = entity.IsDisabled,
            DisplayName = entity.DisplayName,
            Settings = await MapSettingsAsync(entity, cancellationToken),
            ReconciledSettingsFingerprint = entity.ReconciledSettingsFingerprint,
            ReconciledSettings = entity.ReconciledSettingsJson,
        };
    }

    /// <inheritdoc />
    protected override async ValueTask<TeamEntity?> GetEntityOrDefaultAsync(
        Expression<Func<TeamEntity, bool>> predicate,
        CancellationToken cancellationToken
    )
    {
        return GetLocalOrDefault(predicate)
            ?? await DbContext.Teams.SingleOrDefaultAsync(predicate, cancellationToken);
    }

    /// <inheritdoc />
    protected override async ValueTask<TeamEntity?> GetEntityOrDefaultAsync(
        string resourceId,
        CancellationToken cancellationToken
    )
    {
        var normalizedTeamId = Normalize(resourceId);
        return await GetEntityOrDefaultAsync(
            entity => entity.NormalizedTeamId == normalizedTeamId,
            cancellationToken
        );
    }

    /// <inheritdoc />
    protected override async ValueTask<IReadOnlyList<TeamEntity>> GetEntityPageAsync(
        long? afterId,
        int take,
        CancellationToken cancellationToken
    )
    {
        var query = DbContext.Teams.AsQueryable();

        if (afterId is { } id)
        {
            query = query.Where(team => team.Id > id);
        }

        return await query.OrderBy(team => team.Id).Take(take).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    protected override long GetSortKey(TeamEntity entity) => entity.Id;

    /// <inheritdoc />
    public async ValueTask<IReadOnlyDictionary<string, string>> GetConcurrencyTokensAsync(
        IReadOnlyCollection<string> teamIds,
        CancellationToken cancellationToken
    )
    {
        List<string?> normalizedTeamIds = [.. teamIds.Select(Normalize)];
        var rows = await DbContext
            .Teams.AsNoTracking()
            .Where(team => normalizedTeamIds.Contains(team.NormalizedTeamId))
            .Select(team => new { team.TeamId, team.ConcurrencyToken })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            row => row.TeamId,
            row => row.ConcurrencyToken,
            StringComparer.OrdinalIgnoreCase
        );
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<PersistedTeam>> GetManyAsync(
        IReadOnlyCollection<string> teamIds,
        CancellationToken cancellationToken
    )
    {
        List<string?> normalizedTeamIds = [.. teamIds.Select(Normalize)];
        var entities = await DbContext
            .Teams.AsNoTracking()
            .Where(team => normalizedTeamIds.Contains(team.NormalizedTeamId))
            .OrderBy(team => team.Id)
            .ToListAsync(cancellationToken);

        var teams = new List<PersistedTeam>(entities.Count);
        foreach (var entity in entities)
        {
            teams.Add(await MapFromEntityAsync(entity, cancellationToken));
        }

        return teams;
    }

    /// <inheritdoc />
    public async ValueTask<PersistedTeamSettings> GetSettingsAsync(
        string teamId,
        CancellationToken cancellationToken
    )
    {
        return await GetSettingsOrDefaultAsync(teamId, cancellationToken)
            ?? throw new KeyNotFoundException($"The team with TeamId='{teamId}' was not found.");
    }

    /// <inheritdoc />
    public async ValueTask<PersistedTeamSettings?> GetSettingsOrDefaultAsync(
        string teamId,
        CancellationToken cancellationToken
    )
    {
        var teamEntity = await GetEntityOrDefaultAsync(teamId, cancellationToken);
        if (teamEntity is null)
        {
            return null;
        }

        return await MapSettingsAsync(teamEntity, cancellationToken);
    }

    /// <inheritdoc />
    public override async ValueTask AddAsync(
        PersistedTeam persistedTeam,
        CancellationToken cancellationToken
    )
    {
        // Assign the row and settings tokens up front so create can return them without a re-read; the interceptor
        // leaves a pre-seeded insert token intact (ADR-1012). The settings version column stays manual.
        persistedTeam.ConcurrencyToken = NextConcurrencyToken();
        persistedTeam.Settings.ConcurrencyToken = NextConcurrencyToken();

        TeamEntity? parent = null;
        if (persistedTeam.ParentTeamId is { } parentTeamId)
        {
            parent =
                await GetEntityOrDefaultAsync(parentTeamId, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"The parent team with TeamId='{parentTeamId}' does not exist."
                );
        }

        IReadOnlyList<string> teamIds = parent is null
            ? [persistedTeam.TeamId]
            : [.. TeamPaths.Parse(parent.Path), persistedTeam.TeamId];

        if (teamIds.Count > TeamPaths.MaxDepth)
        {
            throw new InvalidOperationException(
                $"The team with TeamId='{persistedTeam.TeamId}' cannot be nested deeper than {TeamPaths.MaxDepth} levels."
            );
        }

        var teamEntity = new TeamEntity
        {
            Id = NextId(),
            TeamId = persistedTeam.TeamId,
            NormalizedTeamId = Normalize(persistedTeam.TeamId),
            ParentTeamId = parent?.Id,
            ParentTeam = parent,
            Path = TeamPaths.Format(teamIds),
            ConcurrencyToken = persistedTeam.ConcurrencyToken,
            SettingsConcurrencyToken = persistedTeam.Settings.ConcurrencyToken,
            IsDisabled = persistedTeam.IsDisabled,
            DisplayName = persistedTeam.DisplayName,
            SettingsJson = persistedTeam.Settings.Value,
            ReconciledSettingsFingerprint = persistedTeam.ReconciledSettingsFingerprint,
            ReconciledSettingsJson = persistedTeam.ReconciledSettings,
            // A collection expression is a fixed-size array, which EF refuses to add to.
            Tenants = new List<TenantEntity>(),
        };

        await DbContext.Teams.AddAsync(teamEntity, cancellationToken);

        persistedTeam.AncestorTeamIds = [.. teamIds.Take(teamIds.Count - 1)];
    }

    /// <inheritdoc />
    public override async ValueTask UpdateAsync(
        PersistedTeam persistedTeam,
        CancellationToken cancellationToken
    )
    {
        var teamId = persistedTeam.TeamId;
        var teamEntity = await GetEntityAsync(teamId, cancellationToken);

        if (
            !string.Equals(
                persistedTeam.ConcurrencyToken,
                teamEntity.ConcurrencyToken,
                StringComparison.Ordinal
            )
        )
        {
            throw new StoreConcurrencyException(
                $"The team with TeamId='{teamId}' has been modified by another process. Please reload and try again."
            );
        }

        // Touch the row so the interceptor regenerates the ConcurrencyToken on save (ADR-1012).
        teamEntity.DisplayName = persistedTeam.DisplayName;
        teamEntity.IsDisabled = persistedTeam.IsDisabled;

        DbContext.Teams.Update(teamEntity);
    }

    /// <inheritdoc />
    public async ValueTask UpdateSettingsAsync(
        PersistedTeamSettings persistedTeamSettings,
        CancellationToken cancellationToken
    )
    {
        var teamId = persistedTeamSettings.TeamId;
        var teamEntity = await GetEntityAsync(teamId, cancellationToken);

        if (
            !string.Equals(
                persistedTeamSettings.ConcurrencyToken,
                teamEntity.SettingsConcurrencyToken,
                StringComparison.Ordinal
            )
        )
        {
            throw new StoreConcurrencyException(
                $"The settings for the team with TeamId='{teamId}' have been modified by another process. Please reload and try again."
            );
        }

        var nextConcurrencyToken = NextConcurrencyToken();

        teamEntity.SettingsConcurrencyToken = nextConcurrencyToken;
        teamEntity.SettingsJson = persistedTeamSettings.Value;

        // A second change before the first is reconciled leaves settings that no pass reconciled, so the next pass has
        // nothing to compare with and reaches every tenant. See ADR-1234.
        if (teamEntity.ReconciledSettingsFingerprint is null)
            teamEntity.ReconciledSettingsJson = null;

        teamEntity.ReconciledSettingsFingerprint = null;

        DbContext.Teams.Update(teamEntity);

        persistedTeamSettings.ConcurrencyToken = nextConcurrencyToken;
    }

    /// <inheritdoc />
    public async ValueTask UpdateReconciledSettingsAsync(
        PersistedTeam persistedTeam,
        CancellationToken cancellationToken
    )
    {
        var teamId = persistedTeam.TeamId;
        var teamEntity = await GetEntityAsync(teamId, cancellationToken);

        if (
            !string.Equals(
                persistedTeam.ConcurrencyToken,
                teamEntity.ConcurrencyToken,
                StringComparison.Ordinal
            )
        )
        {
            throw new StoreConcurrencyException(
                $"The team with TeamId='{teamId}' has been modified by another process. Please reload and try again."
            );
        }

        // Set only these two columns and do not call Update, so that only they are written. The interceptor regenerates
        // the row ConcurrencyToken on save (ADR-1012).
        teamEntity.ReconciledSettingsFingerprint = persistedTeam.ReconciledSettingsFingerprint;
        teamEntity.ReconciledSettingsJson = persistedTeam.ReconciledSettings;
    }

    /// <inheritdoc />
    public async ValueTask<bool> RemoveAsync(string teamId, CancellationToken cancellationToken)
    {
        var teamEntity = await GetEntityOrDefaultAsync(teamId, cancellationToken);
        if (teamEntity is null)
        {
            return false;
        }

        if (await HasDependentsAsync(teamEntity, cancellationToken))
        {
            throw new InvalidOperationException(
                $"The team with TeamId='{teamId}' cannot be removed because it still has one or more tenants or teams."
            );
        }

        var normalizedTeamId = teamEntity.NormalizedTeamId;
        DbContext.RoleAssignments.RemoveRange(
            await DbContext
                .RoleAssignments.IgnoreQueryFilters()
                .Where(assignment =>
                    assignment.ResourceType == OpenIdResourceTypes.Team
                    && assignment.NormalizedResourceId == normalizedTeamId
                )
                .ToListAsync(cancellationToken)
        );

        DbContext.Teams.Remove(teamEntity);

        // A deleted team's identifier is never reissued.
        await DbContext.RetiredTeams.AddAsync(
            new RetiredTeamEntity
            {
                Id = NextId(),
                TeamId = teamEntity.TeamId,
                NormalizedTeamId = teamEntity.NormalizedTeamId,
            },
            cancellationToken
        );

        return true;
    }

    /// <inheritdoc />
    public async ValueTask<bool> HasDependentsAsync(
        string teamId,
        CancellationToken cancellationToken
    )
    {
        var teamEntity = await GetEntityOrDefaultAsync(teamId, cancellationToken);
        if (teamEntity is null)
        {
            return false;
        }

        return await HasDependentsAsync(teamEntity, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<bool> IsTeamIdRetiredAsync(
        string teamId,
        CancellationToken cancellationToken
    )
    {
        var normalizedTeamId = Normalize(teamId);
        return await DbContext.RetiredTeams.AnyAsync(
            entity => entity.NormalizedTeamId == normalizedTeamId,
            cancellationToken
        );
    }

    private async ValueTask<bool> HasDependentsAsync(
        TeamEntity entity,
        CancellationToken cancellationToken
    )
    {
        return await DbContext.Tenants.AnyAsync(
                tenant => tenant.TeamId == entity.Id,
                cancellationToken
            )
            || await DbContext.Teams.AnyAsync(
                team => team.ParentTeamId == entity.Id,
                cancellationToken
            );
    }
}
