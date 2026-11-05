// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Disposables;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Teams;

/// <summary>
/// Provides an abstraction for caching <see cref="OpenIdTeam"/> instances.
/// </summary>
// See ADR-1411
[PublicAPI]
public interface IOpenIdTeamCache
{
    /// <summary>
    /// Attempts to get an <see cref="OpenIdTeam"/> instance from the cache using the specified <paramref name="teamId"/>.
    /// </summary>
    /// <param name="teamId">The identifier of the team to get from the cache.</param>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> instance that can provide additional user-defined information about the current instance or operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="OpenIdTeam"/> instance.</returns>
    ValueTask<AsyncSharedReferenceLease<OpenIdTeam>> TryGetAsync(
        string teamId,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Sets an <see cref="OpenIdTeam"/> instance in the cache using the specified <paramref name="teamId"/>.
    /// </summary>
    /// <param name="teamId">The identifier of the team to set in the cache.</param>
    /// <param name="team">The <see cref="OpenIdTeam"/> instance to set in the cache.</param>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> instance that can provide additional user-defined information about the current instance or operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask SetAsync(
        string teamId,
        AsyncSharedReferenceLease<OpenIdTeam> team,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Removes the <see cref="OpenIdTeam"/> instance cached for the specified <paramref name="teamId"/>, if any, so the
    /// next request for the team materializes it again from the store. A lease already handed out stays usable until
    /// its holder releases it.
    /// </summary>
    /// <param name="teamId">The identifier of the team to remove from the cache.</param>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> instance that can provide additional user-defined information about the current instance or operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask RemoveAsync(
        string teamId,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Removes every cached <see cref="OpenIdTeam"/> instance, so the next request for any team materializes it
    /// again from the store. A lease already handed out stays usable until its holder releases it.
    /// </summary>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> instance that can provide additional user-defined information about the current instance or operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask ClearAsync(IPropertyBag propertyBag, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the revision of every cached team.
    /// </summary>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> instance that can provide additional user-defined information about the current instance or operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="TeamRevision"/> of each cached team.</returns>
    ValueTask<IReadOnlyCollection<TeamRevision>> GetRevisionsAsync(
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    );
}
