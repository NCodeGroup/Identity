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
using NCode.Identity.Secrets.Persistence.Logic;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Provides the default implementation of <see cref="IOpenIdClientResolver"/> that loads the persisted client, applies
/// the tenant-merged effective settings and deserialized secrets, and builds the public <see cref="OpenIdClient"/>.
/// </summary>
internal class DefaultOpenIdClientResolver(
    IStoreManagerFactory storeManagerFactory,
    IOpenIdClientFactory clientFactory,
    ISettingSerializer settingSerializer,
    ISecretSerializer secretSerializer
) : IOpenIdClientResolver
{
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IOpenIdClientFactory ClientFactory { get; } = clientFactory;
    private ISettingSerializer SettingSerializer { get; } = settingSerializer;
    private ISecretSerializer SecretSerializer { get; } = secretSerializer;

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
        )
        {
            return null;
        }

        var jsonOptions = openIdContext.Environment.JsonSerializerOptions;
        var clientSettings = SettingSerializer.DeserializeSettings(
            persistedClient.Settings.Value,
            jsonOptions
        );
        var effectiveSettings = openIdContext.Tenant.SettingsProvider.Collection.Merge(
            clientSettings
        );
        var secrets = SecretSerializer.DeserializeSecrets(persistedClient.Secrets.Value);

        return await ClientFactory.CreatePublicClientAsync(
            openIdContext,
            persistedClient.ClientId,
            effectiveSettings,
            secrets,
            cancellationToken
        );
    }
}
