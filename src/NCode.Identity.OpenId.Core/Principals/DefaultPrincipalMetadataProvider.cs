#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

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
/// principal's metadata inconsistent across the cluster (ADR-0055). Registered as a singleton; it opens a store manager
/// per resolve via <see cref="IStoreManagerFactory"/>, so it holds no scoped state.
/// </summary>
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
