// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Disposables;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Platform;
using NCode.Identity.Results;
using NCode.Persistence.Stores;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Teams;

/// <summary>
/// Provides the default implementation of the <see cref="IOpenIdTeamProvider"/> abstraction: it probes the team
/// cache and materializes a missing team from the store through the <see cref="IOpenIdTeamFactory"/>.
/// </summary>
// See ADR-1411
internal class DefaultOpenIdTeamProvider(
    IStoreManagerFactory storeManagerFactory,
    IOpenIdTeamCache teamCache,
    IOpenIdTeamFactory teamFactory
) : IOpenIdTeamProvider
{
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IOpenIdTeamCache TeamCache { get; } = teamCache;
    private IOpenIdTeamFactory TeamFactory { get; } = teamFactory;

    /// <inheritdoc />
    public async ValueTask<AsyncSharedReferenceLease<OpenIdTeam>> GetTeamAsync(
        OpenIdEnvironment openIdEnvironment,
        OpenIdPlatform openIdPlatform,
        string teamId,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        var cached = await TeamCache.TryGetAsync(teamId, propertyBag, cancellationToken);
        if (cached.IsActive)
            return cached;

        PersistedTeam? persistedTeam;
        await using (var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken))
        {
            var store = storeManager.GetStore<ITeamStore>();
            persistedTeam = await store.GetOrDefaultAsync(teamId, cancellationToken);
        }

        if (persistedTeam is null)
            throw TypedResults
                .NotFound()
                .AsException($"A team with id '{teamId}' could not be found.");

        AsyncSharedReferenceLease<OpenIdTeam> parentTeam = default;
        try
        {
            if (persistedTeam.ParentTeamId is { } parentTeamId)
            {
                // the tree is finite and acyclic, because a team's parent exists before it
                parentTeam = await GetTeamAsync(
                    openIdEnvironment,
                    openIdPlatform,
                    parentTeamId,
                    propertyBag,
                    cancellationToken
                );
            }

            await using var created = await TeamFactory.CreateTeamAsync(
                openIdEnvironment,
                openIdPlatform,
                parentTeam,
                persistedTeam,
                propertyBag,
                cancellationToken
            );

            await TeamCache.SetAsync(teamId, created, propertyBag, cancellationToken);

            return created.AddReference();
        }
        finally
        {
            if (parentTeam.IsActive)
                await parentTeam.DisposeAsync();
        }
    }
}
