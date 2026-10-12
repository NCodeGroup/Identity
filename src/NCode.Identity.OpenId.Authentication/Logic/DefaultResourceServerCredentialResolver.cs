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

using NCode.Identity.OpenId.Authentication.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Identity.Secrets.Logic;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Logic;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Authentication.Logic;

/// <summary>
/// Provides the default implementation of <see cref="IResourceServerCredentialResolver"/>.
/// </summary>
internal class DefaultResourceServerCredentialResolver(
    IStoreManagerFactory storeManagerFactory,
    IAmbientTenantAccessor ambientTenantAccessor,
    ISettingSerializer settingSerializer,
    ISecretSerializer secretSerializer,
    ISecretKeyCollectionProviderFactory secretKeyCollectionProviderFactory
) : IResourceServerCredentialResolver
{
    private const int PageSize = 100;

    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IAmbientTenantAccessor AmbientTenantAccessor { get; } = ambientTenantAccessor;
    private ISettingSerializer SettingSerializer { get; } = settingSerializer;
    private ISecretSerializer SecretSerializer { get; } = secretSerializer;
    private ISecretKeyCollectionProviderFactory SecretKeyCollectionProviderFactory { get; } =
        secretKeyCollectionProviderFactory;

    /// <inheritdoc />
    public async ValueTask<ResourceServerCredentialResolution> ResolveAsync(
        OpenIdContext openIdContext,
        IReadOnlyCollection<string> effectiveScopes,
        CancellationToken cancellationToken
    )
    {
        if (effectiveScopes.Count == 0)
        {
            return new ResourceServerCredentialResolution { ResourceServers = [] };
        }

        var openIdTenant = openIdContext.Tenant;
        var tenantEffectiveSettings = openIdTenant.SettingsProvider.Collection;
        var jsonOptions = openIdContext.Environment.JsonSerializerOptions;

        var requestedScopes =
            effectiveScopes as IReadOnlySet<string>
            ?? effectiveScopes.ToHashSet(StringComparer.Ordinal);

        // The OIDC runtime endpoints do not establish an ambient tenant scope, so set it explicitly here so the
        // persistence-layer tenant query filter applies to the resource-server reads (ADR-0018).
        using var tenantScope = AmbientTenantAccessor.BeginScope(openIdTenant.TenantId);

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var resourceServerStore = storeManager.GetStore<IResourceServerStore>();

        // Keep the matches ordered by audience identifier for a stable `aud` ordering; capture the single match's
        // persisted secrets so a single-audience token can be encrypted to the resource server's own key.
        var matches = new SortedList<string, ResolvedResourceServer>(StringComparer.Ordinal);
        IReadOnlyCollection<PersistedSecret>? lastMatchSecrets = null;

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

                if (!resourceServer.Scopes.Any(scope => requestedScopes.Contains(scope.Value)))
                {
                    continue;
                }

                var effectiveSettings = tenantEffectiveSettings.Merge(
                    SettingSerializer.DeserializeSettings(
                        resourceServer.Settings.Value,
                        jsonOptions
                    )
                );

                matches.Add(
                    resourceServer.Identifier,
                    new ResolvedResourceServer
                    {
                        ResourceServerId = resourceServer.ResourceServerId,
                        Identifier = resourceServer.Identifier,
                        EffectiveSettings = effectiveSettings,
                    }
                );

                lastMatchSecrets = resourceServer.Secrets.Value;
            }

            cursor = page.NextCursor;
        } while (cursor is not null);

        var resourceServers = matches.Values.ToList();

        // The per-audience secret keys only apply to a single-audience token; multi-audience tokens use the tenant's
        // keys (a compact JWE encrypts to one recipient) (ADR-0075). Secrets are deserialized only for the single
        // match so no key material is materialized (and left undisposed) for the multi-audience case.
        var singleSecretsProvider =
            resourceServers.Count == 1 && lastMatchSecrets is not null
                ? SecretKeyCollectionProviderFactory.CreateStatic(
                    SecretSerializer.DeserializeSecrets(lastMatchSecrets)
                )
                : null;

        return new ResourceServerCredentialResolution
        {
            ResourceServers = resourceServers,
            SingleSecretsProvider = singleSecretsProvider,
        };
    }
}
