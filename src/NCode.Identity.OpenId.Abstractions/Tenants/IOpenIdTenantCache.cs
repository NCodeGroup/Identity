// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Disposables;
using NCode.Identity.OpenId.Teams;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Provides an abstraction for caching <see cref="OpenIdTenant"/> instances.
/// </summary>
[PublicAPI]
public interface IOpenIdTenantCache
{
    /// <summary>
    /// Attempts to get an <see cref="OpenIdTenant"/> instance from the cache using the specified <paramref name="tenantId"/>.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant to get from the cache.</param>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> instance that can provide additional user-defined information about the current instance or operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="OpenIdTenant"/> instance.</returns>
    ValueTask<AsyncSharedReferenceLease<OpenIdTenant>> TryGetAsync(
        string tenantId,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Sets an <see cref="OpenIdTenant"/> instance in the cache using the specified <paramref name="tenantId"/>.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant to set in the cache.</param>
    /// <param name="tenant">The <see cref="OpenIdTenant"/> instance to set in the cache.</param>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> instance that can provide additional user-defined information about the current instance or operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask SetAsync(
        string tenantId,
        AsyncSharedReferenceLease<OpenIdTenant> tenant,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Removes the <see cref="OpenIdTenant"/> instance cached for the specified <paramref name="tenantId"/>, if any, so the
    /// next request for the tenant materializes it again from the store. A lease already handed out stays usable until
    /// its holder releases it.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant to remove from the cache.</param>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> instance that can provide additional user-defined information about the current instance or operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask RemoveAsync(
        string tenantId,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Removes every cached <see cref="OpenIdTenant"/> instance of the team with the specified <paramref name="teamId"/>,
    /// so the next request for any of its tenants materializes it again from the store. A lease already handed out
    /// stays usable until its holder releases it.
    /// </summary>
    /// <param name="teamId">The identifier of the team whose tenants to remove from the cache.</param>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> instance that can provide additional user-defined information about the current instance or operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask RemoveTeamAsync(
        string teamId,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Removes every cached <see cref="OpenIdTenant"/> instance, so the next request for any tenant materializes it
    /// again from the store. A lease already handed out stays usable until its holder releases it.
    /// </summary>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> instance that can provide additional user-defined information about the current instance or operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask ClearAsync(IPropertyBag propertyBag, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the distinct revisions of the teams that the cached <see cref="OpenIdTenant"/> instances were materialized under.
    /// </summary>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> instance that can provide additional user-defined information about the current instance or operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="TeamRevision"/> of each such team.</returns>
    ValueTask<IReadOnlyCollection<TeamRevision>> GetTeamRevisionsAsync(
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    );
}
