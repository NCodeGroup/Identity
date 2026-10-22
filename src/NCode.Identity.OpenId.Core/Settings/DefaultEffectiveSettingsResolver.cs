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

using System.Text.Json;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Provides the default implementation of <see cref="IEffectiveSettingsResolver"/>. A scope's document is deserialized
/// and expanded by the <see cref="ISecurityProfileCatalog"/> with the environment's JSON options, then merged onto its
/// parent's effective settings. The server's committed document is read from the store at call time, so the result
/// never depends on this replica's last poll.
/// </summary>
/// <remarks>
/// It depends on <see cref="IOpenIdServerProvider"/>, so the server's own factory and the root settings data source,
/// which the provider builds on, keep calling <see cref="ISecurityProfileCatalog"/> directly.
/// </remarks>
// See ADR-0204.
internal sealed class DefaultEffectiveSettingsResolver(
    IOpenIdEnvironmentProvider openIdEnvironmentProvider,
    IOpenIdServerProvider openIdServerProvider,
    IStoreManagerFactory storeManagerFactory,
    ISecurityProfileCatalog securityProfileCatalog
) : IEffectiveSettingsResolver
{
    private IOpenIdEnvironmentProvider OpenIdEnvironmentProvider { get; } =
        openIdEnvironmentProvider;
    private IOpenIdServerProvider OpenIdServerProvider { get; } = openIdServerProvider;
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private ISecurityProfileCatalog SecurityProfileCatalog { get; } = securityProfileCatalog;

    /// <inheritdoc />
    public IReadOnlyCollection<Setting> ReadScopeSettings(string scopeName, JsonElement settings)
    {
        ArgumentNullException.ThrowIfNull(scopeName);

        return SecurityProfileCatalog.Expand(
            settings,
            scopeName,
            OpenIdEnvironmentProvider.Get().JsonSerializerOptions
        );
    }

    /// <inheritdoc />
    public IReadOnlySettingCollection Resolve(
        IReadOnlySettingCollection parentSettings,
        string scopeName,
        JsonElement settings
    )
    {
        ArgumentNullException.ThrowIfNull(parentSettings);
        ArgumentNullException.ThrowIfNull(scopeName);

        return parentSettings.Merge(ReadScopeSettings(scopeName, settings));
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlySettingCollection> ResolveServerAsync(
        JsonElement settings,
        CancellationToken cancellationToken
    )
    {
        var server = await OpenIdServerProvider.GetAsync(
            OpenIdEnvironmentProvider.Get(),
            cancellationToken
        );

        return Resolve(
            server.ConfiguredSettingsProvider.Collection,
            OpenIdSettingScopes.Server,
            settings
        );
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlySettingCollection> ResolveServerAsync(
        CancellationToken cancellationToken
    )
    {
        var server = await OpenIdServerProvider.GetAsync(
            OpenIdEnvironmentProvider.Get(),
            cancellationToken
        );
        var configuredSettings = server.ConfiguredSettingsProvider.Collection;

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IServerStore>();

        var serverSettings = await store.GetSettingsOrDefaultAsync(
            server.ServerId,
            cancellationToken
        );
        if (serverSettings is null)
        {
            return configuredSettings;
        }

        return Resolve(configuredSettings, OpenIdSettingScopes.Server, serverSettings.Value);
    }
}
