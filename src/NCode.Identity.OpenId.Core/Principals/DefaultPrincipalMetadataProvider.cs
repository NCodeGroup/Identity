// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Json;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Principals;

/// <summary>
/// Provides the default implementation of <see cref="IPrincipalMetadataProvider"/>, which resolves a principal's
/// metadata bags from the <see cref="IFederatedPrincipalStore"/> (where they are inlined on the principal). It reads
/// fresh on every call so the result is always current and cluster-consistent — the single source of truth is the
/// store, not a per-node cache (an authorization-relevant <c>SystemMetadata</c> edit therefore takes effect on the next
/// issuance across every node, matching Auth0). A host that wants to trade freshness for fewer reads replaces this
/// provider with a decorator over a <em>distributed</em> cache; a per-node in-memory cache would make the same
/// principal's metadata inconsistent across the cluster. Registered as a singleton; it opens a store manager
/// per resolve via <see cref="IStoreManagerFactory"/>, so it holds no scoped state.
/// </summary>
// See ADR-1055.
internal sealed class DefaultPrincipalMetadataProvider(IStoreManagerFactory storeManagerFactory)
    : IPrincipalMetadataProvider
{
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;

    /// <inheritdoc />
    public async ValueTask<PrincipalMetadata> GetMetadataAsync(
        OpenIdContext openIdContext,
        string principalId,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrEmpty(principalId))
        {
            return PrincipalMetadata.Empty;
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IFederatedPrincipalStore>();

        var principal = await store.GetOrDefaultAsync(principalId, cancellationToken);
        if (principal is null)
        {
            return PrincipalMetadata.Empty;
        }

        return new PrincipalMetadata(
            principal.ProfileMetadata.OrEmptyObject(),
            principal.SystemMetadata.OrEmptyObject()
        );
    }
}
