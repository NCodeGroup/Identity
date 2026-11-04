// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using IdGen;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NCode.Collections.Providers;
using NCode.Collections.Providers.PeriodicPolling;
using NCode.Disposables;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Platform;

/// <summary>
/// Provides a default implementation of the <see cref="IOpenIdPlatformFactory"/> abstraction.
/// </summary>
/// <remarks>
/// The server's persisted settings document is loaded through <see cref="ISecurityProfileCatalog"/>, like every other
/// scope's, so the security profiles it selects contribute their ceilings at the top of the cascade.
/// </remarks>
// See ADR-1148 (security profiles accumulate down the cascade).
internal class DefaultOpenIdPlatformFactory(
    IOptions<OpenIdOptions> optionsAccessor,
    IServiceProvider serviceProvider,
    ISecretSerializer secretSerializer,
    ISecretProtectorProvider secretProtectorProvider,
    IStoreManagerFactory storeManagerFactory,
    ICollectionDataSourceFactory collectionDataSourceFactory,
    IReadOnlySettingCollectionProviderFactory settingCollectionProviderFactory,
    ISecretKeyCollectionProviderFactory secretKeyCollectionProviderFactory,
    ISecurityProfileCatalog securityProfileCatalog,
    IIdGenerator<long> idGenerator
) : IOpenIdPlatformFactory
{
    // Read on use: Value runs the OpenIdOptions validators, which construct the client-authentication handlers, which depend on this factory (ADR-1204).
    private IOptions<OpenIdOptions> OptionsAccessor { get; } = optionsAccessor;
    private OpenIdOptions Options => OptionsAccessor.Value;
    private IServiceProvider ServiceProvider { get; } = serviceProvider;
    private ISecretSerializer SecretSerializer { get; } = secretSerializer;
    private ISecretProtectorProvider SecretProtectorProvider { get; } = secretProtectorProvider;
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private ICollectionDataSourceFactory CollectionDataSourceFactory { get; } =
        collectionDataSourceFactory;
    private IReadOnlySettingCollectionProviderFactory SettingCollectionProviderFactory { get; } =
        settingCollectionProviderFactory;
    private ISecretKeyCollectionProviderFactory SecretKeyCollectionProviderFactory { get; } =
        secretKeyCollectionProviderFactory;
    private ISecurityProfileCatalog SecurityProfileCatalog { get; } = securityProfileCatalog;
    private IIdGenerator<long> IdGenerator { get; } = idGenerator;

    /// <summary>
    /// Gets the <c>PlatformId</c> from the configurable options, or uses the default if not set.
    /// </summary>
    protected virtual string GetPlatformId()
    {
        var platformId = Options.Platform.PlatformId;

        if (string.IsNullOrEmpty(platformId))
        {
            platformId = OpenIdPlatformOptions.DefaultPlatformId;
        }

        return platformId;
    }

    /// <inheritdoc />
    public async ValueTask<OpenIdPlatform> CreateAsync(
        OpenIdEnvironment openIdEnvironment,
        CancellationToken cancellationToken
    )
    {
        var platformId = GetPlatformId();

        var disposables = new List<object>(3);
        var propertyBag = openIdEnvironment.PropertyBag.Clone();
        var persistedPlatform = await GetPersistedPlatformAsync(
            platformId,
            propertyBag,
            cancellationToken
        );
        try
        {
            var rootSettingsDataSource = await CreateRootSettingsDataSourceAsync(
                propertyBag,
                cancellationToken
            );

            // Until the merged provider owns them, a failure disposes the data sources that exist.
            var dataSources = new List<object>(2) { rootSettingsDataSource };
            RefreshableSettingsCollectionDataSource serverSettingsDataSource;
            IReadOnlySettingCollectionProvider settingsProvider;
            try
            {
                serverSettingsDataSource = await CreatePlatformSettingsDataSourceAsync(
                    openIdEnvironment,
                    persistedPlatform,
                    propertyBag,
                    cancellationToken
                );
                dataSources.Add(serverSettingsDataSource);

                // The merged provider owns both data sources; the configured provider shares the root.
                settingsProvider = SettingCollectionProviderFactory.Create(
                    [rootSettingsDataSource, serverSettingsDataSource],
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

            var secretsProvider = await CreateSecretsProviderAsync(
                persistedPlatform,
                propertyBag,
                cancellationToken
            );
            disposables.Add(secretsProvider);

            return Create(
                platformId,
                configuredSettingsProvider,
                settingsProvider,
                secretsProvider,
                serverSettingsDataSource.RefreshAsync,
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
        string platformId,
        IReadOnlySettingCollectionProvider configuredSettingsProvider,
        IReadOnlySettingCollectionProvider settingsProvider,
        ISecretKeyCollectionProvider secretsProvider,
        Func<CancellationToken, ValueTask> refreshSettingsAsync,
        IPropertyBag propertyBag
    ) =>
        new DefaultOpenIdPlatform(
            platformId,
            configuredSettingsProvider,
            settingsProvider,
            secretsProvider,
            refreshSettingsAsync,
            propertyBag
        );

    /// <summary>
    /// Attempts to retrieve the <see cref="PersistedPlatform"/> with the specified server ID from the store.
    /// If not found, creates a new instance and persists it.
    /// </summary>
    protected internal virtual async ValueTask<PersistedPlatform> GetPersistedPlatformAsync(
        string platformId,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IPlatformStore>();

        var persistedPlatform = await store.GetOrDefaultAsync(platformId, cancellationToken);
        if (persistedPlatform is not null)
            return persistedPlatform;

        persistedPlatform = CreateEmptyPersistedPlatform(platformId);

        await store.AddAsync(persistedPlatform, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        return persistedPlatform;
    }

    /// <summary>
    /// Creates an empty <see cref="PersistedPlatform"/> instance with the specified server ID.
    /// </summary>
    protected internal virtual PersistedPlatform CreateEmptyPersistedPlatform(string platformId)
    {
        var settings = new PersistedPlatformSettings
        {
            PlatformId = platformId,
            ConcurrencyToken = Guid.NewGuid().ToString("N"),
            Value = JsonSerializer.SerializeToElement(null, typeof(object)),
        };

        var secrets = new PersistedPlatformSecrets
        {
            PlatformId = platformId,
            ConcurrencyToken = Guid.NewGuid().ToString("N"),
            Value = [],
        };

        return new PersistedPlatform
        {
            PlatformId = platformId,
            ConcurrencyToken = Guid.NewGuid().ToString("N"),
            DisplayName = platformId,
            Settings = settings,
            Secrets = secrets,
        };
    }

    /// <summary>
    /// Used to create a data source for the server's settings that are derived from the configurable options.
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
    /// Used to create a data source for the server's settings that are persisted in a store.
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
        var platformId = persistedPlatform.PlatformId;

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IPlatformStore>();

        var newSettings = await store.GetSettingsOrDefaultAsync(platformId, cancellationToken);
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

    /// <summary>
    /// Used to create the <see cref="ISecretKeyCollectionProvider"/> containing secrets only known to the OpenID server.
    /// </summary>
    protected internal virtual async ValueTask<ISecretKeyCollectionProvider> CreateSecretsProviderAsync(
        PersistedPlatform persistedPlatform,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var protector = await SecretProtectorProvider.GetProtectorForSecretsAsync(
            SecretOwner.ForPlatform(persistedPlatform.PlatformId),
            persistedPlatform.Secrets.Value,
            cancellationToken
        );

        var initialSecrets = SecretSerializer.DeserializeSecrets(
            protector,
            persistedPlatform.Secrets.Value
        );

        var dataSource = CollectionDataSourceFactory.CreatePeriodicPolling(
            persistedPlatform,
            initialSecrets,
            Options.Platform.SecretsPeriodicRefreshInterval,
            RefreshSecretsAsync
        );

        var provider = SecretKeyCollectionProviderFactory.Create(dataSource, owns: true);

        return provider;
    }

    private async ValueTask<RefreshCollectionResult<SecretKey>> RefreshSecretsAsync(
        PersistedPlatform persistedPlatform,
        IReadOnlyCollection<SecretKey> current,
        CancellationToken cancellationToken
    )
    {
        var platformId = persistedPlatform.PlatformId;

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IPlatformStore>();

        var newSecrets = await store.GetSecretsOrDefaultAsync(platformId, cancellationToken);
        if (newSecrets is null)
            return RefreshCollectionResultFactory.Unchanged<SecretKey>();

        var prevSecrets = persistedPlatform.Secrets;
        var prevConcurrencyToken = prevSecrets.ConcurrencyToken;
        var newConcurrencyToken = newSecrets.ConcurrencyToken;

        if (string.Equals(prevConcurrencyToken, newConcurrencyToken, StringComparison.Ordinal))
            return RefreshCollectionResultFactory.Unchanged<SecretKey>();

        var protector = await SecretProtectorProvider.GetProtectorForSecretsAsync(
            SecretOwner.ForPlatform(platformId),
            newSecrets.Value,
            cancellationToken
        );

        var secrets = SecretSerializer.DeserializeSecrets(protector, newSecrets.Value);

        // update the state after successfully deserializing the secrets
        persistedPlatform.Secrets = newSecrets;

        return RefreshCollectionResultFactory.Changed(secrets);
    }
}
