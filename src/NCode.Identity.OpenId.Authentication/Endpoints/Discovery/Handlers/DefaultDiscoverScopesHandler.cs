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

using NCode.Identity.OpenId.Authentication.Endpoints.Discovery.Commands;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Identity.OpenId.Settings;
using NCode.Mediator;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Discovery.Handlers;

/// <summary>
/// Contributes the <c>scopes_supported</c> discovery metadata, derived from the scopes defined on the tenant's
/// enabled resource servers (ADR-0026).
/// </summary>
internal class DefaultDiscoverScopesHandler(
    IStoreManagerFactory storeManagerFactory,
    IAmbientTenantAccessor ambientTenantAccessor
) : ICommandHandler<DiscoverMetadataCommand>, ISupportMediatorPriority
{
    private const int PageSize = 100;

    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IAmbientTenantAccessor AmbientTenantAccessor { get; } = ambientTenantAccessor;

    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.Low;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        DiscoverMetadataCommand command,
        CancellationToken cancellationToken
    )
    {
        var (openIdContext, metadata, _) = command;

        // The OIDC runtime does not establish an ambient tenant scope, so set it explicitly here so the
        // persistence-layer tenant query filter applies to the resource-server reads (ADR-0018).
        using var tenantScope = AmbientTenantAccessor.BeginScope(openIdContext.Tenant.TenantId);

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var resourceServerStore = storeManager.GetStore<IResourceServerStore>();

        var scopes = new SortedSet<string>(StringComparer.Ordinal);

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

                foreach (var scope in resourceServer.Scopes)
                {
                    scopes.Add(scope.Value);
                }
            }

            cursor = page.NextCursor;
        } while (cursor is not null);

        metadata[OpenIdSettingNames.ScopesSupported] = scopes.ToList();
    }
}
