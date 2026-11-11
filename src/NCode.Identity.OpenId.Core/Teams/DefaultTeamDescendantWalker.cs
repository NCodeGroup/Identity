// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Teams;

/// <summary>
/// Provides a default implementation of <see cref="ITeamDescendantWalker"/> that walks a team's subtree depth first, in
/// pre-order, reading the child teams of each team one page at a time, each page in a unit of work of its own.
/// </summary>
// See ADR-1455.
internal sealed class DefaultTeamDescendantWalker(IStoreManagerFactory storeManagerFactory)
    : ITeamDescendantWalker
{
    internal const int PageSize = 100;

    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;

    /// <inheritdoc />
    public async ValueTask WalkDescendantsAsync(
        string teamId,
        Func<PersistedTeam, CancellationToken, ValueTask<bool>> visitAsync,
        CancellationToken cancellationToken
    )
    {
        var page = new PageRequest { Limit = PageSize };
        do
        {
            var result = await GetChildPageAsync(teamId, page, cancellationToken);
            foreach (var child in result.Items)
            {
                // The recursion is at most TeamPaths.MaxDepth deep, and each level holds one page of child teams.
                if (await visitAsync(child, cancellationToken))
                    await WalkDescendantsAsync(child.TeamId, visitAsync, cancellationToken);
            }

            page = page with { Cursor = result.NextCursor };
        } while (page.Cursor is not null);
    }

    private async ValueTask<PagedResult<PersistedTeam>> GetChildPageAsync(
        string teamId,
        PageRequest page,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITeamStore>();
        return await store.GetChildPageAsync(teamId, page, cancellationToken);
    }
}
