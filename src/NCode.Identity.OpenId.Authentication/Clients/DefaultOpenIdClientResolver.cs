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

using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Authentication.Endpoints;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets.Persistence;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Provides the default implementation of <see cref="IOpenIdClientResolver"/> that loads the persisted client, applies
/// the tenant-merged effective settings and deserialized secrets, and builds the public <see cref="OpenIdClient"/>. A
/// client bound to a resource server is resolved only at an endpoint that accepts such clients
/// (<see cref="IOpenIdEndpointResourceServerClientMetadata"/>); everywhere else it does not exist, so its credentials
/// serve only the calls the resource server needs.
/// </summary>
// See ADR-0168.
internal class DefaultOpenIdClientResolver(
    IStoreManagerFactory storeManagerFactory,
    IOpenIdClientFactory clientFactory,
    ISecretSerializer secretSerializer,
    ISecurityProfileCatalog securityProfileCatalog
) : IOpenIdClientResolver
{
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IOpenIdClientFactory ClientFactory { get; } = clientFactory;
    private ISecretSerializer SecretSerializer { get; } = secretSerializer;
    private ISecurityProfileCatalog SecurityProfileCatalog { get; } = securityProfileCatalog;

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

        var jsonOptions = openIdContext.Environment.JsonSerializerOptions;
        var clientSettings = SecurityProfileCatalog.Expand(
            persistedClient.Settings.Value,
            OpenIdSettingScopes.Client,
            jsonOptions
        );
        var effectiveSettings = openIdContext.Tenant.SettingsProvider.Collection.Merge(
            clientSettings
        );
        var secrets = SecretSerializer.DeserializeSecrets(persistedClient.Secrets.Value);

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
