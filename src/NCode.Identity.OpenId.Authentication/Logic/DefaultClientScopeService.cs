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

        // Scope values are case-sensitive per RFC 6749.
        var allowedScopes = new HashSet<string>(StringComparer.Ordinal);

        string? cursor = null;
        do
        {
            var page = await resourceServerStore.GetPageAsync(cursor, PageSize, cancellationToken);

            foreach (var resourceServer in page.Items)
            {
                if (resourceServer.IsDisabled)
                {
                    continue;
                }

                var resourceServerScopes = resourceServer.Scopes.Select(scope => scope.Value);

                if (resourceServer.IsSystem)
                {
                    // A system resource server is implicitly available to every client (ADR-0025).
                    allowedScopes.UnionWith(resourceServerScopes);
                }
                else if (
                    grantedScopesByResourceServer.TryGetValue(
                        resourceServer.ResourceServerId,
                        out var grantedScopes
                    )
                )
                {
                    // Read-time intersection: a granted scope no longer defined on the resource server is ignored.
                    allowedScopes.UnionWith(resourceServerScopes.Where(grantedScopes.Contains));
                }
            }

            cursor = page.NextCursor;
        } while (cursor is not null);

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

        var audiences = new SortedSet<string>(StringComparer.Ordinal);

        string? cursor = null;
        do
        {
            var page = await resourceServerStore.GetPageAsync(cursor, PageSize, cancellationToken);

            foreach (var resourceServer in page.Items)
            {
                if (resourceServer.IsDisabled)
                {
                    continue;
                }

                if (resourceServer.Scopes.Any(scope => requestedScopes.Contains(scope.Value)))
                {
                    audiences.Add(resourceServer.Identifier);
                }
            }

            cursor = page.NextCursor;
        } while (cursor is not null);

        return audiences;
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
                cursor,
                PageSize,
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
