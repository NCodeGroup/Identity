// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Collections.Providers;
using NCode.Collections.Providers.PeriodicPolling;
using NCode.Disposables;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Persistence;
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
/// its document, read at the team scope, over its parent team's settings, or the platform's for a top-level team, and
/// refresh periodically from the store.
/// </summary>
// See ADR-1411
// See ADR-1426
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
        AsyncSharedReferenceLease<OpenIdTeam> parentTeam,
        PersistedTeam persistedTeam,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        var parentSettingsProvider = parentTeam.IsActive
            ? parentTeam.Value.SettingsProvider
            : openIdPlatform.SettingsProvider;

        var initialSettings = EffectiveSettingsResolver.ReadScopeSettings(
            parentSettingsProvider.Collection,
            OpenIdSettingScopes.Team,
            persistedTeam.Settings.Value
        );

        var periodicPollingSource = CollectionDataSourceFactory.CreatePeriodicPolling(
            new RefreshSettingsState(parentSettingsProvider, persistedTeam),
            initialSettings,
            OptionsAccessor.Value.Team.SettingsPeriodicRefreshInterval,
            RefreshSettingsAsync
        );

        var dataSources = new List<ICollectionDataSource<Setting>>
        {
            parentSettingsProvider.AsDataSource(),
            periodicPollingSource,
        };

        var provider = SettingCollectionProviderFactory.Create(dataSources, owns: true);

        await using var settingsReference = provider.AsSharedReference();

        OpenIdTeam team = new DefaultOpenIdTeam(
            persistedTeam.TeamId,
            persistedTeam.ConcurrencyToken,
            persistedTeam.DisplayName,
            persistedTeam.IsDisabled || (parentTeam.IsActive && parentTeam.Value.IsDisabled),
            TeamPaths.Format([.. persistedTeam.AncestorTeamIds, persistedTeam.TeamId]),
            settingsReference,
            parentTeam.IsActive ? parentTeam.AddReference() : default,
            propertyBag
        );

        return team.AsSharedReference();
    }

    private readonly record struct RefreshSettingsState(
        IReadOnlySettingCollectionProvider ParentSettingsProvider,
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
            state.ParentSettingsProvider.Collection,
            OpenIdSettingScopes.Team,
            newSettings.Value
        );

        // update the state after successfully deserializing the settings
        persistedTeam.Settings = newSettings;

        return RefreshCollectionResultFactory.Changed(settings);
    }
}
