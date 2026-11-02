// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Authentication.Endpoints.Discovery.Commands;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Mediator;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Discovery.Handlers;

/// <summary>
/// Contributes the <c>scopes_supported</c> discovery metadata, derived from the scopes defined on the tenant's
/// enabled resource servers.
/// </summary>
internal class DefaultDiscoverScopesHandler(
    IStoreManagerFactory storeManagerFactory,
    IAmbientTenantAccessor ambientTenantAccessor
) : ICommandHandler<DiscoverMetadataCommand>, ISupportMediatorPriority
{
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
        var (openIdContext, metadata) = command;

        // The OIDC runtime does not establish an ambient tenant scope, so set it explicitly here so the
        // persistence-layer tenant query filter applies to the resource-server reads (ADR-1018).
        using var tenantScope = AmbientTenantAccessor.BeginScope(openIdContext.Tenant.TenantId);

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var resourceServerStore = storeManager.GetStore<IResourceServerStore>();

        var enabledScopes = await resourceServerStore.GetEnabledScopesAsync(
            scopeValues: null,
            cancellationToken
        );

        var scopes = new SortedSet<string>(
            enabledScopes.Select(scope => scope.Value),
            StringComparer.Ordinal
        );

        // A discovery metadata field, not a setting: the scope catalog is the tenant's resource servers (ADR-1026).
        metadata[OpenIdProtocol.MetadataNames.ScopesSupported] = scopes.ToList();
    }
}
