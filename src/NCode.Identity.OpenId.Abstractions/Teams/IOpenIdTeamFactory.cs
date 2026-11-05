// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Disposables;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Platform;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Teams;

/// <summary>
/// Provides the ability to create <see cref="OpenIdTeam"/> instances from persisted teams.
/// </summary>
// See ADR-1411
[PublicAPI]
public interface IOpenIdTeamFactory
{
    /// <summary>
    /// Creates a new <see cref="OpenIdTeam"/> instance whose settings layer the team's document over the platform's.
    /// </summary>
    /// <param name="openIdEnvironment">The <see cref="OpenIdEnvironment"/> instance associated with the current operation.</param>
    /// <param name="openIdPlatform">The <see cref="OpenIdPlatform"/> instance associated with the current operation.</param>
    /// <param name="persistedTeam">The <see cref="PersistedTeam"/> to materialize.</param>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> that can provide additional user-defined information about the current instance or operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the newly created <see cref="OpenIdTeam"/> instance.</returns>
    ValueTask<AsyncSharedReferenceLease<OpenIdTeam>> CreateTeamAsync(
        OpenIdEnvironment openIdEnvironment,
        OpenIdPlatform openIdPlatform,
        PersistedTeam persistedTeam,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    );
}
