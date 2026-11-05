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
/// its parent's effective settings. The platform's committed document is read from the store at call time, so the
/// result never depends on this replica's last poll.
/// </summary>
/// <remarks>
/// It depends on <see cref="IOpenIdPlatformProvider"/>, so the platform's own factory and the root settings data
/// source, which the provider builds on, keep calling <see cref="ISecurityProfileCatalog"/> directly.
/// </remarks>
// See ADR-1204.
internal sealed class DefaultEffectiveSettingsResolver(
    IOpenIdEnvironmentProvider openIdEnvironmentProvider,
    IOpenIdPlatformProvider openIdPlatformProvider,
    IStoreManagerFactory storeManagerFactory,
    ISettingSerializer settingSerializer,
    ITenantEnvironmentCatalog tenantEnvironmentCatalog,
    ISecurityProfileCatalog securityProfileCatalog
) : IEffectiveSettingsResolver
{
    private IOpenIdEnvironmentProvider OpenIdEnvironmentProvider { get; } =
        openIdEnvironmentProvider;
    private IOpenIdPlatformProvider OpenIdPlatformProvider { get; } = openIdPlatformProvider;
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
        var platform = await OpenIdPlatformProvider.GetAsync(
            OpenIdEnvironmentProvider.Get(),
            cancellationToken
        );

        return Resolve(
            platform.ConfiguredSettingsProvider.Collection,
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
        var platform = await OpenIdPlatformProvider.GetAsync(
            OpenIdEnvironmentProvider.Get(),
            cancellationToken
        );
        var configuredSettings = platform.ConfiguredSettingsProvider.Collection;

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IPlatformStore>();

        var platformSettings = await store.GetSettingsOrDefaultAsync(cancellationToken);
        if (platformSettings is null)
        {
            return configuredSettings;
        }

        return ResolveStored(
            configuredSettings,
            OpenIdSettingScopes.Platform,
            OpenIdConstants.PlatformResourceId,
            platformSettings.Value
        );
    }

    /// <inheritdoc />
    // See ADR-1411.
    public async ValueTask<IReadOnlySettingCollection> ResolveTeamAsync(
        string teamId,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(teamId);

        var platformSettings = await ResolvePlatformAsync(cancellationToken);

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITeamStore>();

        var teamSettings =
            await store.GetSettingsOrDefaultAsync(teamId, cancellationToken)
            ?? throw new KeyNotFoundException($"The team '{teamId}' does not exist.");

        return ResolveStored(
            platformSettings,
            OpenIdSettingScopes.Team,
            teamId,
            teamSettings.Value
        );
    }

    /// <inheritdoc />
    // See ADR-1411.
    public async ValueTask<IReadOnlySettingCollection> ResolveTeamAsync(
        string teamId,
        JsonElement settings,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(teamId);

        var platformSettings = await ResolvePlatformAsync(cancellationToken);

        return Resolve(platformSettings, OpenIdSettingScopes.Team, settings);
    }
}
