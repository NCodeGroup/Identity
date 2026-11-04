// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Teams;

/// <summary>
/// Provides the default implementation of <see cref="ITeamProvisioner"/>. It reads the team and, when it is missing,
/// adds it with empty settings; a write that loses to a concurrent creator returns the team that creator wrote.
/// </summary>
// See ADR-1403
internal sealed class DefaultTeamProvisioner(IStoreManagerFactory storeManagerFactory)
    : ITeamProvisioner
{
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;

    /// <inheritdoc />
    public async ValueTask<PersistedTeam> EnsureCreatedAsync(
        string teamId,
        string displayName,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITeamStore>();

        var existing = await store.GetOrDefaultAsync(teamId, cancellationToken);
        if (existing is not null)
            return existing;

        var persistedTeam = new PersistedTeam
        {
            TeamId = teamId,
            DisplayName = displayName,
            IsDisabled = false,
            Settings = new PersistedTeamSettings
            {
                TeamId = teamId,
                ConcurrencyToken = Guid.NewGuid().ToString("N"),
                Value = JsonSerializer.SerializeToElement(null, typeof(object)),
            },
            ConcurrencyToken = Guid.NewGuid().ToString("N"),
        };
        await store.AddAsync(persistedTeam, cancellationToken);

        try
        {
            await storeManager.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Another node or request may have created the team between the read above and this write. The row it
            // wrote is the team; anything else is a genuine failure.
            var concurrent = await FindTeamAsync(teamId, cancellationToken);
            if (concurrent is null)
                throw;

            return concurrent;
        }

        return persistedTeam;
    }

    private async ValueTask<PersistedTeam?> FindTeamAsync(
        string teamId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITeamStore>();
        return await store.GetOrDefaultAsync(teamId, cancellationToken);
    }
}
