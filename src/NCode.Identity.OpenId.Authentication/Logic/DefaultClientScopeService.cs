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

using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Authentication.Logic;

/// <summary>
/// Provides the default implementation of <see cref="IClientScopeService"/>.
/// </summary>
internal class DefaultClientScopeService(
    IStoreManagerFactory storeManagerFactory,
    IAmbientTenantAccessor ambientTenantAccessor
) : IClientScopeService
{
    private const int PageSize = 100;

    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IAmbientTenantAccessor AmbientTenantAccessor { get; } = ambientTenantAccessor;

    /// <inheritdoc />
    public async ValueTask<IReadOnlyCollection<string>> GetAllowedScopesAsync(
        string tenantId,
        string clientId,
        CancellationToken cancellationToken
    )
    {
        // The OIDC runtime endpoints do not establish an ambient tenant scope, so set it explicitly here so the
        // persistence-layer tenant query filter applies to the resource-server and client-grant reads (ADR-0018).
        using var tenantScope = AmbientTenantAccessor.BeginScope(tenantId);

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var resourceServerStore = storeManager.GetStore<IResourceServerStore>();
        var clientGrantStore = storeManager.GetStore<IClientGrantStore>();

        var grantedScopesByResourceServer = await LoadClientGrantsAsync(
            clientGrantStore,
            clientId,
            cancellationToken
        );

        // The DBMS returns only the enabled resource servers that are system-owned or that this client holds a grant
        // for; everything else cannot contribute an allowed scope (ADR-0080).
        var projections = await resourceServerStore.GetEnabledScopeProjectionsBySystemOrIdsAsync(
            grantedScopesByResourceServer.Keys.ToList(),
            cancellationToken
        );

        // Scope values are case-sensitive per RFC 6749.
        var allowedScopes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var resourceServer in projections)
        {
            if (resourceServer.IsSystem)
            {
                // A system resource server is implicitly available to every client (ADR-0025).
                allowedScopes.UnionWith(resourceServer.ScopeValues);
            }
            else if (
                grantedScopesByResourceServer.TryGetValue(
                    resourceServer.ResourceServerId,
                    out var grantedScopes
                )
            )
            {
                // Read-time intersection: a granted scope no longer defined on the resource server is ignored.
                allowedScopes.UnionWith(resourceServer.ScopeValues.Where(grantedScopes.Contains));
            }
        }

        return allowedScopes;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyCollection<string>> ResolveAudiencesAsync(
        string tenantId,
        IReadOnlyCollection<string> scopes,
        CancellationToken cancellationToken
    )
    {
        if (scopes.Count == 0)
        {
            return [];
        }

        var requestedScopes =
            scopes as IReadOnlySet<string> ?? scopes.ToHashSet(StringComparer.Ordinal);

        // The OIDC runtime endpoints do not establish an ambient tenant scope, so set it explicitly here so the
        // persistence-layer tenant query filter applies to the resource-server reads (ADR-0018).
        using var tenantScope = AmbientTenantAccessor.BeginScope(tenantId);

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var resourceServerStore = storeManager.GetStore<IResourceServerStore>();

        // The DBMS pre-filters to enabled resource servers owning one of the scopes (case-insensitive); the exact
        // case-sensitive ownership match is applied here (ADR-0080).
        var projections = await resourceServerStore.GetEnabledScopeProjectionsByScopesAsync(
            scopes,
            cancellationToken
        );

        var audiences = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var resourceServer in projections)
        {
            if (resourceServer.ScopeValues.Any(requestedScopes.Contains))
            {
                audiences.Add(resourceServer.Identifier);
            }
        }

        return audiences;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyCollection<string>> GetEnabledResourceServerIdentifiersAsync(
        string tenantId,
        CancellationToken cancellationToken
    )
    {
        // The OIDC runtime endpoints do not establish an ambient tenant scope, so set it explicitly here so the
        // persistence-layer tenant query filter applies to the resource-server reads (ADR-0018).
        using var tenantScope = AmbientTenantAccessor.BeginScope(tenantId);

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var resourceServerStore = storeManager.GetStore<IResourceServerStore>();

        var identifiers = await resourceServerStore.GetEnabledIdentifiersAsync(cancellationToken);

        return identifiers as IReadOnlySet<string> ?? identifiers.ToHashSet(StringComparer.Ordinal);
    }

    private static async ValueTask<Dictionary<string, HashSet<string>>> LoadClientGrantsAsync(
        IClientGrantStore clientGrantStore,
        string clientId,
        CancellationToken cancellationToken
    )
    {
        var grantedScopesByResourceServer = new Dictionary<string, HashSet<string>>(
            StringComparer.Ordinal
        );

        string? cursor = null;
        do
        {
            var page = await clientGrantStore.GetPageAsync(
                clientId,
                new PageRequest { Cursor = cursor, Limit = PageSize },
                cancellationToken
            );

            foreach (var grant in page.Items)
            {
                grantedScopesByResourceServer[grant.ResourceServerId] = grant.Scopes.ToHashSet(
                    StringComparer.Ordinal
                );
            }

            cursor = page.NextCursor;
        } while (cursor is not null);

        return grantedScopesByResourceServer;
    }
}
