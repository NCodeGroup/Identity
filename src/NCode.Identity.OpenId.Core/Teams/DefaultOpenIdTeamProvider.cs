// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Disposables;
using NCode.Identity.OpenId.Environments;
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
// See ADR-1403
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

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITeamStore>();

        var persistedTeam = await store.GetOrDefaultAsync(teamId, cancellationToken);
        if (persistedTeam is null)
            throw TypedResults
                .NotFound()
                .AsException($"A team with id '{teamId}' could not be found.");

        await using var created = await TeamFactory.CreateTeamAsync(
            openIdEnvironment,
            openIdPlatform,
            persistedTeam,
            propertyBag,
            cancellationToken
        );

        await TeamCache.SetAsync(teamId, created, propertyBag, cancellationToken);

        return created.AddReference();
    }
}
