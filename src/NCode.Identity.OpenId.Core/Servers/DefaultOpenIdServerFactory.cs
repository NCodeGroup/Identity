#region Copyright Preamble

// Copyright @ 2024 NCode Group
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
using NCode.Identity.Settings;
using NCode.Persistence.Stores;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Servers;

/// <summary>
/// Provides a default implementation of the <see cref="IOpenIdServerFactory"/> abstraction.
/// </summary>
/// <remarks>
/// The server's persisted settings document is loaded through <see cref="ISecurityProfileCatalog"/>, like every other
/// scope's, so the security profiles it selects contribute their ceilings at the top of the cascade.
/// </remarks>
// See ADR-0148 (security profiles accumulate down the cascade).
internal class DefaultOpenIdServerFactory(
    IOptions<OpenIdOptions> optionsAccessor,
    IServiceProvider serviceProvider,
    ISecretSerializer secretSerializer,
    IStoreManagerFactory storeManagerFactory,
    ICollectionDataSourceFactory collectionDataSourceFactory,
    IReadOnlySettingCollectionProviderFactory settingCollectionProviderFactory,
    ISecretKeyCollectionProviderFactory secretKeyCollectionProviderFactory,
    ISecurityProfileCatalog securityProfileCatalog,
    IIdGenerator<long> idGenerator
) : IOpenIdServerFactory
{
    // Read on use: Value runs the OpenIdOptions validators, which construct the client-authentication handlers, which depend on this factory (ADR-0204).
    private IOptions<OpenIdOptions> OptionsAccessor { get; } = optionsAccessor;
    private OpenIdOptions Options => OptionsAccessor.Value;
    private IServiceProvider ServiceProvider { get; } = serviceProvider;
    private ISecretSerializer SecretSerializer { get; } = secretSerializer;
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
    /// Gets the <c>ServerId</c> from the configurable options, or uses the default if not set.
    /// </summary>
    protected virtual string GetServerId()
    {
        var serverId = Options.Server.ServerId;

        if (string.IsNullOrEmpty(serverId))
        {
            serverId = OpenIdServerOptions.DefaultServerId;
        }

        return serverId;
    }

    /// <inheritdoc />
    public async ValueTask<OpenIdServer> CreateAsync(
        OpenIdEnvironment openIdEnvironment,
        CancellationToken cancellationToken
    )
    {
        var serverId = GetServerId();

        var disposables = new List<object>(3);
        var propertyBag = openIdEnvironment.PropertyBag.Clone();
        var persistedServer = await GetPersistedServerAsync(
            serverId,
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
                serverSettingsDataSource = await CreateServerSettingsDataSourceAsync(
                    openIdEnvironment,
                    persistedServer,
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
                persistedServer,
                propertyBag,
                cancellationToken
            );
            disposables.Add(secretsProvider);

            return Create(
                serverId,
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
    /// Factory method to create an instance of <see cref="OpenIdServer"/>.
    /// </summary>
    protected internal virtual OpenIdServer Create(
        string serverId,
        IReadOnlySettingCollectionProvider configuredSettingsProvider,
        IReadOnlySettingCollectionProvider settingsProvider,
        ISecretKeyCollectionProvider secretsProvider,
        Func<CancellationToken, ValueTask> refreshSettingsAsync,
        IPropertyBag propertyBag
    ) =>
        new DefaultOpenIdServer(
            serverId,
            configuredSettingsProvider,
            settingsProvider,
            secretsProvider,
            refreshSettingsAsync,
            propertyBag
        );

    /// <summary>
    /// Attempts to retrieve the <see cref="PersistedServer"/> with the specified server ID from the store.
    /// If not found, creates a new instance and persists it.
    /// </summary>
    protected internal virtual async ValueTask<PersistedServer> GetPersistedServerAsync(
        string serverId,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IServerStore>();

        var persistedServer = await store.GetOrDefaultAsync(serverId, cancellationToken);
        if (persistedServer is not null)
            return persistedServer;

        persistedServer = CreateEmptyPersistedServer(serverId);

        await store.AddAsync(persistedServer, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        return persistedServer;
    }

    /// <summary>
    /// Creates an empty <see cref="PersistedServer"/> instance with the specified server ID.
    /// </summary>
    protected internal virtual PersistedServer CreateEmptyPersistedServer(string serverId)
    {
        var settings = new PersistedServerSettings
        {
            ServerId = serverId,
            ConcurrencyToken = Guid.NewGuid().ToString("N"),
            Value = JsonSerializer.SerializeToElement(null, typeof(object)),
        };

        var secrets = new PersistedServerSecrets
        {
            ServerId = serverId,
            ConcurrencyToken = Guid.NewGuid().ToString("N"),
            Value = [],
        };

        return new PersistedServer
        {
            ServerId = serverId,
            ConcurrencyToken = Guid.NewGuid().ToString("N"),
            DisplayName = serverId,
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
    protected internal virtual ValueTask<RefreshableSettingsCollectionDataSource> CreateServerSettingsDataSourceAsync(
        OpenIdEnvironment openIdEnvironment,
        PersistedServer persistedServer,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var jsonOptions = openIdEnvironment.JsonSerializerOptions;
        var initialSettings = SecurityProfileCatalog.Expand(
            persistedServer.Settings.Value,
            OpenIdSettingScopes.Server,
            jsonOptions
        );

        Func<CancellationToken, ValueTask<IReadOnlyCollection<Setting>?>> loadChangedSettingsAsync =
            ct => LoadChangedSettingsAsync(openIdEnvironment, persistedServer, ct);

        // A committed change is read at once through OpenIdServer.RefreshSettingsAsync; the interval is the fallback (ADR-0219).
        var dataSource = ActivatorUtilities.CreateInstance<RefreshableSettingsCollectionDataSource>(
            ServiceProvider,
            initialSettings,
            loadChangedSettingsAsync,
            Options.Server.SettingsPeriodicRefreshInterval
        );

        return ValueTask.FromResult(dataSource);
    }

    private async ValueTask<IReadOnlyCollection<Setting>?> LoadChangedSettingsAsync(
        OpenIdEnvironment openIdEnvironment,
        PersistedServer persistedServer,
        CancellationToken cancellationToken
    )
    {
        var serverId = persistedServer.ServerId;

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IServerStore>();

        var newSettings = await store.GetSettingsOrDefaultAsync(serverId, cancellationToken);
        if (newSettings is null)
            return null;

        var prevSettings = persistedServer.Settings;
        var prevConcurrencyToken = prevSettings.ConcurrencyToken;
        var newConcurrencyToken = newSettings.ConcurrencyToken;

        if (string.Equals(prevConcurrencyToken, newConcurrencyToken, StringComparison.Ordinal))
            return null;

        var jsonOptions = openIdEnvironment.JsonSerializerOptions;
        var settings = SecurityProfileCatalog.Expand(
            newSettings.Value,
            OpenIdSettingScopes.Server,
            jsonOptions
        );

        // update the state after successfully deserializing the settings
        persistedServer.Settings = newSettings;

        return settings;
    }

    /// <summary>
    /// Used to create the <see cref="ISecretKeyCollectionProvider"/> containing secrets only known to the OpenID server.
    /// </summary>
    protected internal virtual ValueTask<ISecretKeyCollectionProvider> CreateSecretsProviderAsync(
        PersistedServer persistedServer,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var initialSecrets = SecretSerializer.DeserializeSecrets(persistedServer.Secrets.Value);

        var dataSource = CollectionDataSourceFactory.CreatePeriodicPolling(
            persistedServer,
            initialSecrets,
            Options.Server.SecretsPeriodicRefreshInterval,
            RefreshSecretsAsync
        );

        var provider = SecretKeyCollectionProviderFactory.Create(dataSource, owns: true);

        return ValueTask.FromResult(provider);
    }

    private async ValueTask<RefreshCollectionResult<SecretKey>> RefreshSecretsAsync(
        PersistedServer persistedServer,
        IReadOnlyCollection<SecretKey> current,
        CancellationToken cancellationToken
    )
    {
        var serverId = persistedServer.ServerId;

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IServerStore>();

        var newSecrets = await store.GetSecretsOrDefaultAsync(serverId, cancellationToken);
        if (newSecrets is null)
            return RefreshCollectionResultFactory.Unchanged<SecretKey>();

        var prevSecrets = persistedServer.Secrets;
        var prevConcurrencyToken = prevSecrets.ConcurrencyToken;
        var newConcurrencyToken = newSecrets.ConcurrencyToken;

        if (string.Equals(prevConcurrencyToken, newConcurrencyToken, StringComparison.Ordinal))
            return RefreshCollectionResultFactory.Unchanged<SecretKey>();

        var secrets = SecretSerializer.DeserializeSecrets(newSecrets.Value);

        // update the state after successfully deserializing the secrets
        persistedServer.Secrets = newSecrets;

        return RefreshCollectionResultFactory.Changed(secrets);
    }
}
