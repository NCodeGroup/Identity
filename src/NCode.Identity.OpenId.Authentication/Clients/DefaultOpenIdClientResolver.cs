// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Authentication.Endpoints;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Provides the default implementation of <see cref="IOpenIdClientResolver"/> that loads the persisted client, applies
/// the tenant-merged effective settings and the deserialized unexpired keys and shared-secret verifiers, and builds the public
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
    /// <summary>
    /// Selects the secrets that have not expired, because a credential past its expiry does not authenticate the
    /// client.
    /// </summary>
    /// <remarks>
    /// A secret whose expiry equals <paramref name="now"/> has expired. Client resolution runs on every
    /// client-authenticated request, so the collection is copied only when one of its secrets has expired.
    /// </remarks>
    /// <param name="secrets">The client's persisted secrets.</param>
    /// <param name="now">The current instant.</param>
    /// <returns>The secrets whose expiry is after <paramref name="now"/>.</returns>
    // See ADR-1320.
    private static IReadOnlyCollection<PersistedSecret> SelectUnexpired(
        IReadOnlyCollection<PersistedSecret> secrets,
        DateTimeOffset now
    )
    {
        if (secrets.Count == 0)
        {
            return secrets;
        }

        var expiredCount = 0;
        foreach (var secret in secrets)
        {
            if (secret.ExpiresWhen <= now)
            {
                expiredCount++;
            }
        }

        if (expiredCount == 0)
        {
            return secrets;
        }

        var unexpired = new List<PersistedSecret>(secrets.Count - expiredCount);
        foreach (var secret in secrets)
        {
            if (secret.ExpiresWhen > now)
            {
                unexpired.Add(secret);
            }
        }

        return unexpired;
    }

    /// <summary>
    /// Separates the secrets that hold one-way verifiers from the secrets that hold key material.
    /// </summary>
    /// <remarks>
    /// Client resolution runs on every client-authenticated request, so no collection is copied when the secrets hold
    /// only one kind.
    /// </remarks>
    /// <param name="secrets">The client's unexpired persisted secrets.</param>
    /// <param name="keys">The secrets that hold key material.</param>
    /// <param name="verifiers">The secrets that hold verifiers.</param>
    // See ADR-1391.
    private static void SplitVerifiers(
        IReadOnlyCollection<PersistedSecret> secrets,
        out IReadOnlyCollection<PersistedSecret> keys,
        out IReadOnlyCollection<PersistedSecret> verifiers
    )
    {
        var verifierCount = 0;
        foreach (var secret in secrets)
        {
            if (SecretEncodings.IsVerifier(secret.SecretEncoding))
            {
                verifierCount++;
            }
        }

        if (verifierCount == 0)
        {
            keys = secrets;
            verifiers = [];
            return;
        }

        if (verifierCount == secrets.Count)
        {
            keys = [];
            verifiers = secrets;
            return;
        }

        var keyList = new List<PersistedSecret>(secrets.Count - verifierCount);
        var verifierList = new List<PersistedSecret>(verifierCount);
        foreach (var secret in secrets)
        {
            if (SecretEncodings.IsVerifier(secret.SecretEncoding))
            {
                verifierList.Add(secret);
            }
            else
            {
                keyList.Add(secret);
            }
        }

        keys = keyList;
        verifiers = verifierList;
    }

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

        var now = TimeProvider.GetUtcNow();
        var liveSecrets = SelectUnexpired(persistedClient.Secrets.Value, now);

        var protector = await SecretProtectorProvider.GetProtectorForSecretsAsync(
            SecretOwner.ForTenant(tenantId),
            liveSecrets,
            cancellationToken
        );
        // A shared secret the server only compares is stored as a one-way verifier, which is not key material, so it is
        // read apart from the client's keys. See ADR-1391.
        SplitVerifiers(liveSecrets, out var keySecrets, out var verifierSecrets);
        var secrets = SecretSerializer.DeserializeSecrets(protector, keySecrets);
        var verifiers = SecretSerializer.DeserializeVerifiers(protector, verifierSecrets);

        var definition = new OpenIdClientDefinition
        {
            ClientId = persistedClient.ClientId,
            Settings = effectiveSettings,
            Secrets = secrets,
            SecretVerifiers = verifiers,
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
