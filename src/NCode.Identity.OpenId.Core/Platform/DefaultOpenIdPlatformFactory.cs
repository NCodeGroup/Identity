// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NCode.Collections.Providers;
using NCode.Disposables;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Platform;

/// <summary>
/// Provides a default implementation of the <see cref="IOpenIdPlatformFactory"/> abstraction.
/// </summary>
/// <remarks>
/// The platform's persisted settings document is loaded through <see cref="ISecurityProfileCatalog"/>, like every other
/// scope's, so the security profiles it selects contribute their ceilings at the top of the cascade.
/// </remarks>
// See ADR-1148 (security profiles accumulate down the cascade).
internal class DefaultOpenIdPlatformFactory(
    IOptions<OpenIdOptions> optionsAccessor,
    IServiceProvider serviceProvider,
    IStoreManagerFactory storeManagerFactory,
    ICollectionDataSourceFactory collectionDataSourceFactory,
    IReadOnlySettingCollectionProviderFactory settingCollectionProviderFactory,
    ISecurityProfileCatalog securityProfileCatalog
) : IOpenIdPlatformFactory
{
    // Read on use: Value runs the OpenIdOptions validators, which construct the client-authentication handlers, which depend on this factory (ADR-1204).
    private IOptions<OpenIdOptions> OptionsAccessor { get; } = optionsAccessor;
    private OpenIdOptions Options => OptionsAccessor.Value;
    private IServiceProvider ServiceProvider { get; } = serviceProvider;
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private ICollectionDataSourceFactory CollectionDataSourceFactory { get; } =
        collectionDataSourceFactory;
    private IReadOnlySettingCollectionProviderFactory SettingCollectionProviderFactory { get; } =
        settingCollectionProviderFactory;
    private ISecurityProfileCatalog SecurityProfileCatalog { get; } = securityProfileCatalog;

    /// <inheritdoc />
    public async ValueTask<OpenIdPlatform> CreateAsync(
        OpenIdEnvironment openIdEnvironment,
        CancellationToken cancellationToken
    )
    {
        var disposables = new List<object>(2);
        var propertyBag = openIdEnvironment.PropertyBag.Clone();
        var persistedPlatform = await GetPersistedPlatformAsync(propertyBag, cancellationToken);
        try
        {
            var rootSettingsDataSource = await CreateRootSettingsDataSourceAsync(
                propertyBag,
                cancellationToken
            );

            // Until the merged provider owns them, a failure disposes the data sources that exist.
            var dataSources = new List<object>(2) { rootSettingsDataSource };
            RefreshableSettingsCollectionDataSource platformSettingsDataSource;
            IReadOnlySettingCollectionProvider settingsProvider;
            try
            {
                platformSettingsDataSource = await CreatePlatformSettingsDataSourceAsync(
                    openIdEnvironment,
                    persistedPlatform,
                    propertyBag,
                    cancellationToken
                );
                dataSources.Add(platformSettingsDataSource);

                // The merged provider owns both data sources; the configured provider shares the root.
                settingsProvider = SettingCollectionProviderFactory.Create(
                    [rootSettingsDataSource, platformSettingsDataSource],
                    owns: true
                );
            }
            catch
            {
                await dataSources.DisposeAllAsync();
                throw;
            }

            disposables.Add(settingsProvider);

            var configuredSettingsProvider = SettingCollectionProviderFactory.Create(
                [rootSettingsDataSource],
                owns: false
            );
            disposables.Add(configuredSettingsProvider);

            return Create(
                configuredSettingsProvider,
                settingsProvider,
                platformSettingsDataSource.RefreshAsync,
                propertyBag
            );
        }
        catch
        {
            await disposables.DisposeAllAsync();
            throw;
        }
    }

    /// <summary>
    /// Factory method to create an instance of <see cref="OpenIdPlatform"/>.
    /// </summary>
    protected internal virtual OpenIdPlatform Create(
        IReadOnlySettingCollectionProvider configuredSettingsProvider,
        IReadOnlySettingCollectionProvider settingsProvider,
        Func<CancellationToken, ValueTask> refreshSettingsAsync,
        IPropertyBag propertyBag
    ) =>
        new DefaultOpenIdPlatform(
            configuredSettingsProvider,
            settingsProvider,
            refreshSettingsAsync,
            propertyBag
        );

    /// <summary>
    /// Attempts to retrieve the <see cref="PersistedPlatform"/> from the store.
    /// If not found, creates a new instance and persists it.
    /// </summary>
    protected internal virtual async ValueTask<PersistedPlatform> GetPersistedPlatformAsync(
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IPlatformStore>();

        var persistedPlatform = await store.GetOrDefaultAsync(cancellationToken);
        if (persistedPlatform is not null)
            return persistedPlatform;

        persistedPlatform = CreateEmptyPersistedPlatform();

        await store.AddAsync(persistedPlatform, cancellationToken);

        try
        {
            await storeManager.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Another replica may have created the platform between the read above and this write — two replicas
            // reaching a fresh deployment, say. The row it wrote is the platform; anything else is a genuine failure.
            var concurrent = await FindPersistedPlatformAsync(cancellationToken);
            if (concurrent is null)
                throw;

            return concurrent;
        }

        return persistedPlatform;
    }

    private async ValueTask<PersistedPlatform?> FindPersistedPlatformAsync(
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IPlatformStore>();
        return await store.GetOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Creates an empty <see cref="PersistedPlatform"/> instance.
    /// </summary>
    protected internal virtual PersistedPlatform CreateEmptyPersistedPlatform()
    {
        var settings = new PersistedPlatformSettings
        {
            ConcurrencyToken = Guid.NewGuid().ToString("N"),
            Value = JsonSerializer.SerializeToElement(null, typeof(object)),
        };

        return new PersistedPlatform
        {
            ConcurrencyToken = Guid.NewGuid().ToString("N"),
            Settings = settings,
        };
    }

    /// <summary>
    /// Used to create a data source for the platform's settings that are derived from the configurable options.
    /// </summary>
    protected internal virtual ValueTask<
        IDisposableCollectionDataSource<Setting>
    > CreateRootSettingsDataSourceAsync(
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var dataSource = ActivatorUtilities.CreateInstance<RootSettingsCollectionDataSource>(
            ServiceProvider
        );

        return ValueTask.FromResult<IDisposableCollectionDataSource<Setting>>(dataSource);
    }

    /// <summary>
    /// Used to create a data source for the platform's settings that are persisted in a store.
    /// </summary>
    protected internal virtual ValueTask<RefreshableSettingsCollectionDataSource> CreatePlatformSettingsDataSourceAsync(
        OpenIdEnvironment openIdEnvironment,
        PersistedPlatform persistedPlatform,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var jsonOptions = openIdEnvironment.JsonSerializerOptions;
        var initialSettings = SecurityProfileCatalog.Expand(
            persistedPlatform.Settings.Value,
            OpenIdSettingScopes.Platform,
            jsonOptions
        );

        Func<CancellationToken, ValueTask<IReadOnlyCollection<Setting>?>> loadChangedSettingsAsync =
            ct => LoadChangedSettingsAsync(openIdEnvironment, persistedPlatform, ct);

        // A committed change is read at once through OpenIdPlatform.RefreshSettingsAsync; the interval is the fallback (ADR-1219).
        var dataSource = ActivatorUtilities.CreateInstance<RefreshableSettingsCollectionDataSource>(
            ServiceProvider,
            initialSettings,
            loadChangedSettingsAsync,
            Options.Platform.SettingsPeriodicRefreshInterval
        );

        return ValueTask.FromResult(dataSource);
    }

    private async ValueTask<IReadOnlyCollection<Setting>?> LoadChangedSettingsAsync(
        OpenIdEnvironment openIdEnvironment,
        PersistedPlatform persistedPlatform,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IPlatformStore>();

        var newSettings = await store.GetSettingsOrDefaultAsync(cancellationToken);
        if (newSettings is null)
            return null;

        var prevSettings = persistedPlatform.Settings;
        var prevConcurrencyToken = prevSettings.ConcurrencyToken;
        var newConcurrencyToken = newSettings.ConcurrencyToken;

        if (string.Equals(prevConcurrencyToken, newConcurrencyToken, StringComparison.Ordinal))
            return null;

        var jsonOptions = openIdEnvironment.JsonSerializerOptions;
        var settings = SecurityProfileCatalog.Expand(
            newSettings.Value,
            OpenIdSettingScopes.Platform,
            jsonOptions
        );

        // update the state after successfully deserializing the settings
        persistedPlatform.Settings = newSettings;

        return settings;
    }
}
