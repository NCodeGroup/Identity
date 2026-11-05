// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Collections.Providers;
using NCode.Collections.Providers.PeriodicPolling;
using NCode.Disposables;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Platform;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Teams;

/// <summary>
/// Provides the default implementation of the <see cref="IOpenIdTeamFactory"/> abstraction. A team's settings layer
/// its document, read at the team scope, over the platform's settings, and refresh periodically from the store.
/// </summary>
// See ADR-1411
internal class DefaultOpenIdTeamFactory(
    IOptions<OpenIdOptions> optionsAccessor,
    IStoreManagerFactory storeManagerFactory,
    IReadOnlySettingCollectionProviderFactory settingCollectionProviderFactory,
    ICollectionDataSourceFactory collectionDataSourceFactory,
    IEffectiveSettingsResolver effectiveSettingsResolver
) : IOpenIdTeamFactory
{
    private IOptions<OpenIdOptions> OptionsAccessor { get; } = optionsAccessor;
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IReadOnlySettingCollectionProviderFactory SettingCollectionProviderFactory { get; } =
        settingCollectionProviderFactory;
    private ICollectionDataSourceFactory CollectionDataSourceFactory { get; } =
        collectionDataSourceFactory;
    private IEffectiveSettingsResolver EffectiveSettingsResolver { get; } =
        effectiveSettingsResolver;

    /// <inheritdoc />
    public async ValueTask<AsyncSharedReferenceLease<OpenIdTeam>> CreateTeamAsync(
        OpenIdEnvironment openIdEnvironment,
        OpenIdPlatform openIdPlatform,
        PersistedTeam persistedTeam,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        var initialSettings = EffectiveSettingsResolver.ReadScopeSettings(
            openIdPlatform.SettingsProvider.Collection,
            OpenIdSettingScopes.Team,
            persistedTeam.Settings.Value
        );

        var periodicPollingSource = CollectionDataSourceFactory.CreatePeriodicPolling(
            new RefreshSettingsState(openIdPlatform, persistedTeam),
            initialSettings,
            OptionsAccessor.Value.Team.SettingsPeriodicRefreshInterval,
            RefreshSettingsAsync
        );

        var dataSources = new List<ICollectionDataSource<Setting>>
        {
            openIdPlatform.SettingsProvider.AsDataSource(),
            periodicPollingSource,
        };

        var provider = SettingCollectionProviderFactory.Create(dataSources, owns: true);

        await using var settingsReference = provider.AsSharedReference();

        OpenIdTeam team = new DefaultOpenIdTeam(
            persistedTeam.TeamId,
            persistedTeam.DisplayName,
            persistedTeam.IsDisabled,
            settingsReference,
            propertyBag
        );

        return team.AsSharedReference();
    }

    private readonly record struct RefreshSettingsState(
        OpenIdPlatform OpenIdPlatform,
        PersistedTeam PersistedTeam
    );

    private async ValueTask<RefreshCollectionResult<Setting>> RefreshSettingsAsync(
        RefreshSettingsState state,
        IReadOnlyCollection<Setting> current,
        CancellationToken cancellationToken
    )
    {
        var persistedTeam = state.PersistedTeam;

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITeamStore>();

        var newSettings = await store.GetSettingsAsync(persistedTeam.TeamId, cancellationToken);

        var prevConcurrencyToken = persistedTeam.Settings.ConcurrencyToken;
        var newConcurrencyToken = newSettings.ConcurrencyToken;

        if (string.Equals(prevConcurrencyToken, newConcurrencyToken, StringComparison.Ordinal))
            return RefreshCollectionResultFactory.Unchanged<Setting>();

        var settings = EffectiveSettingsResolver.ReadScopeSettings(
            state.OpenIdPlatform.SettingsProvider.Collection,
            OpenIdSettingScopes.Team,
            newSettings.Value
        );

        // update the state after successfully deserializing the settings
        persistedTeam.Settings = newSettings;

        return RefreshCollectionResultFactory.Changed(settings);
    }
}
