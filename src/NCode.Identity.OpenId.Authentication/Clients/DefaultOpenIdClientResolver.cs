// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Authentication.Endpoints;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Provides the default implementation of <see cref="IOpenIdClientResolver"/> that loads the persisted client, applies
/// the tenant-merged effective settings and deserialized unexpired secrets, and builds the public
/// <see cref="OpenIdClient"/>. A client bound to a resource server is resolved only at an endpoint that accepts such
/// clients (<see cref="IOpenIdEndpointResourceServerClientMetadata"/>); everywhere else it does not exist, so its
/// credentials serve only the calls the resource server needs.
/// </summary>
// See ADR-1168.
internal class DefaultOpenIdClientResolver(
    IStoreManagerFactory storeManagerFactory,
    IOpenIdClientFactory clientFactory,
    ISecretSerializer secretSerializer,
    ISecretProtectorProvider secretProtectorProvider,
    IEffectiveSettingsResolver effectiveSettingsResolver,
    TimeProvider timeProvider
) : IOpenIdClientResolver
{
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IOpenIdClientFactory ClientFactory { get; } = clientFactory;
    private ISecretSerializer SecretSerializer { get; } = secretSerializer;
    private ISecretProtectorProvider SecretProtectorProvider { get; } = secretProtectorProvider;
    private IEffectiveSettingsResolver EffectiveSettingsResolver { get; } =
        effectiveSettingsResolver;
    private TimeProvider TimeProvider { get; } = timeProvider;

    /// <inheritdoc />
    public async ValueTask<OpenIdClient?> ResolveClientOrDefaultAsync(
        OpenIdContext openIdContext,
        string clientId,
        CancellationToken cancellationToken
    )
    {
        var tenantId = openIdContext.Tenant.TenantId;

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IClientStore>();
        var persistedClient = await store.GetOrDefaultAsync(clientId, cancellationToken);

        if (
            persistedClient is null
            || persistedClient.IsDisabled
            || !string.Equals(tenantId, persistedClient.TenantId, StringComparison.Ordinal)
            || !IsAcceptedHere(openIdContext, persistedClient)
        )
        {
            return null;
        }

        var effectiveSettings = EffectiveSettingsResolver.Resolve(
            openIdContext.Tenant.SettingsProvider.Collection,
            OpenIdSettingScopes.Client,
            persistedClient.Settings.Value
        );

        // A credential past its expiry no longer authenticates the client (ADR-1320).
        var now = TimeProvider.GetUtcNow();
        var liveSecrets = persistedClient
            .Secrets.Value.Where(secret => secret.ExpiresWhen > now)
            .ToList();

        var protector = await SecretProtectorProvider.GetProtectorForSecretsAsync(
            SecretOwner.ForTenant(tenantId),
            liveSecrets,
            cancellationToken
        );
        var secrets = SecretSerializer.DeserializeSecrets(protector, liveSecrets);

        var definition = new OpenIdClientDefinition
        {
            ClientId = persistedClient.ClientId,
            Settings = effectiveSettings,
            Secrets = secrets,
            ResourceServerId = persistedClient.ResourceServerId,
        };

        return await ClientFactory.CreatePublicClientAsync(
            openIdContext,
            definition,
            cancellationToken
        );
    }

    // An ordinary client is accepted everywhere; a client bound to a resource server only at an endpoint that opts in,
    // so a new endpoint refuses it unless it states otherwise (RFC 9701 §3).
    private static bool IsAcceptedHere(OpenIdContext openIdContext, PersistedClient persistedClient)
    {
        if (persistedClient.ResourceServerId is null)
        {
            return true;
        }

        var metadata = openIdContext
            .Http.GetEndpoint()
            ?.Metadata.GetMetadata<IOpenIdEndpointResourceServerClientMetadata>();

        return metadata is { AcceptsResourceServerClients: true };
    }
}
