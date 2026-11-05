// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Disposables;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Platform;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Teams;

/// <summary>
/// Provides the materialized <see cref="OpenIdTeam"/> for a team identifier, shared through the team cache.
/// </summary>
// See ADR-1411
[PublicAPI]
public interface IOpenIdTeamProvider
{
    /// <summary>
    /// Gets the <see cref="OpenIdTeam"/> with the specified identifier, from the cache or else materialized from the store.
    /// </summary>
    /// <param name="openIdEnvironment">The <see cref="OpenIdEnvironment"/> instance associated with the current operation.</param>
    /// <param name="openIdPlatform">The <see cref="OpenIdPlatform"/> instance associated with the current operation.</param>
    /// <param name="teamId">The identifier of the team to get.</param>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> that can provide additional user-defined information about the current instance or operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing a lease on the <see cref="OpenIdTeam"/> instance.</returns>
    /// <remarks>A team that does not exist is answered with a 404 result exception.</remarks>
    ValueTask<AsyncSharedReferenceLease<OpenIdTeam>> GetTeamAsync(
        OpenIdEnvironment openIdEnvironment,
        OpenIdPlatform openIdPlatform,
        string teamId,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    );
}
