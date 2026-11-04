// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Platform;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Provides the default implementation of <see cref="IEffectiveSettingsResolver"/>. A scope's document is read
/// with the environment's JSON options, given the security profiles its environment type implies by the
/// <see cref="ITenantEnvironmentCatalog"/>, and expanded by the <see cref="ISecurityProfileCatalog"/>, then merged onto
/// its parent's effective settings. The server's committed document is read from the store at call time, so the result
/// never depends on this replica's last poll.
/// </summary>
/// <remarks>
/// It depends on <see cref="IOpenIdPlatformProvider"/>, so the server's own factory and the root settings data source,
/// which the provider builds on, keep calling <see cref="ISecurityProfileCatalog"/> directly.
/// </remarks>
// See ADR-1204.
internal sealed class DefaultEffectiveSettingsResolver(
    IOpenIdEnvironmentProvider openIdEnvironmentProvider,
    IOpenIdPlatformProvider openIdServerProvider,
    IStoreManagerFactory storeManagerFactory,
    ISettingSerializer settingSerializer,
    ITenantEnvironmentCatalog tenantEnvironmentCatalog,
    ISecurityProfileCatalog securityProfileCatalog
) : IEffectiveSettingsResolver
{
    private IOpenIdEnvironmentProvider OpenIdEnvironmentProvider { get; } =
        openIdEnvironmentProvider;
    private IOpenIdPlatformProvider OpenIdServerProvider { get; } = openIdServerProvider;
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private ISettingSerializer SettingSerializer { get; } = settingSerializer;
    private ITenantEnvironmentCatalog TenantEnvironmentCatalog { get; } = tenantEnvironmentCatalog;
    private ISecurityProfileCatalog SecurityProfileCatalog { get; } = securityProfileCatalog;

    /// <inheritdoc />
    public IReadOnlyCollection<Setting> ReadScopeSettings(
        IReadOnlySettingCollection parentSettings,
        string scopeName,
        JsonElement settings
    )
    {
        ArgumentNullException.ThrowIfNull(parentSettings);
        ArgumentNullException.ThrowIfNull(scopeName);

        var scopeSettings = SettingSerializer.DeserializeSettings(
            settings,
            scopeName,
            OpenIdEnvironmentProvider.Get().JsonSerializerOptions
        );
        var impliedSettings = TenantEnvironmentCatalog.SelectImpliedProfiles(
            parentSettings,
            scopeName,
            scopeSettings
        );

        return SecurityProfileCatalog.Expand(impliedSettings, scopeName);
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

        return parentSettings.Merge(ReadScopeSettings(parentSettings, scopeName, settings));
    }

    /// <inheritdoc />
    // See ADR-1245.
    public IReadOnlySettingCollection ResolveStored(
        IReadOnlySettingCollection parentSettings,
        string scopeName,
        string ownerId,
        JsonElement settings
    )
    {
        ArgumentNullException.ThrowIfNull(ownerId);

        try
        {
            return Resolve(parentSettings, scopeName, settings);
        }
        catch (JsonException exception)
        {
            throw new UnreadableSettingsException(scopeName, ownerId, exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlySettingCollection> ResolvePlatformAsync(
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
            OpenIdSettingScopes.Platform,
            settings
        );
    }

    /// <inheritdoc />
    // See ADR-1245.
    public async ValueTask<IReadOnlySettingCollection> ResolvePlatformAsync(
        CancellationToken cancellationToken
    )
    {
        var server = await OpenIdServerProvider.GetAsync(
            OpenIdEnvironmentProvider.Get(),
            cancellationToken
        );
        var configuredSettings = server.ConfiguredSettingsProvider.Collection;

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IPlatformStore>();

        var serverSettings = await store.GetSettingsOrDefaultAsync(cancellationToken);
        if (serverSettings is null)
        {
            return configuredSettings;
        }

        return ResolveStored(
            configuredSettings,
            OpenIdSettingScopes.Platform,
            OpenIdConstants.PlatformResourceId,
            serverSettings.Value
        );
    }
}
