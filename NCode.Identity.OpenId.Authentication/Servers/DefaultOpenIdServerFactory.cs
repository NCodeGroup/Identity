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
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NCode.Collections.Providers;
using NCode.Collections.Providers.PeriodicPolling;
using NCode.Disposables;
using NCode.Identity.OpenId.Authentication.Options;
using NCode.Identity.OpenId.Authentication.Settings;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Persistence.Logic;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Authentication.Servers;

/// <summary>
/// Provides a default implementation of the <see cref="IOpenIdServerFactory"/> abstraction.
/// </summary>
[PublicAPI]
public class DefaultOpenIdServerFactory(
    IOptions<OpenIdOptions> optionsAccessor,
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    ISettingSerializer settingSerializer,
    ISecretSerializer secretSerializer,
    IStoreManagerFactory storeManagerFactory,
    ICollectionDataSourceFactory collectionDataSourceFactory,
    IReadOnlySettingCollectionProviderFactory settingCollectionProviderFactory,
    ISecretKeyCollectionProviderFactory secretKeyCollectionProviderFactory,
    IIdGenerator<long> idGenerator
) : IOpenIdServerFactory
{
    private OpenIdOptions Options { get; } = optionsAccessor.Value;
    private IServiceProvider ServiceProvider { get; } = serviceProvider;
    private IConfiguration Configuration { get; } = configuration;
    private ISettingSerializer SettingSerializer { get; } = settingSerializer;
    private ISecretSerializer SecretSerializer { get; } = secretSerializer;
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private ICollectionDataSourceFactory CollectionDataSourceFactory { get; } = collectionDataSourceFactory;
    private IReadOnlySettingCollectionProviderFactory SettingCollectionProviderFactory { get; } = settingCollectionProviderFactory;
    private ISecretKeyCollectionProviderFactory SecretKeyCollectionProviderFactory { get; } = secretKeyCollectionProviderFactory;
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

        var disposables = new List<object>(2);
        var propertyBag = openIdEnvironment.PropertyBag.Clone();
        var persistedServer = await GetPersistedServerAsync(serverId, propertyBag, cancellationToken);
        try
        {
            var settingsProvider = await CreateSettingsProviderAsync(openIdEnvironment, persistedServer, propertyBag, cancellationToken);
            disposables.Add(settingsProvider);

            var secretsProvider = await CreateSecretsProviderAsync(persistedServer, propertyBag, cancellationToken);
            disposables.Add(secretsProvider);

            return Create(serverId, settingsProvider, secretsProvider, propertyBag);
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
        IReadOnlySettingCollectionProvider settingsProvider,
        ISecretKeyCollectionProvider secretsProvider,
        IPropertyBag propertyBag
    ) => new DefaultOpenIdServer(
        serverId,
        settingsProvider,
        secretsProvider,
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
            Value = JsonSerializer.SerializeToElement(null, typeof(object))
        };

        var secrets = new PersistedServerSecrets
        {
            ServerId = serverId,
            ConcurrencyToken = Guid.NewGuid().ToString("N"),
            Value = []
        };

        return new PersistedServer
        {
            ServerId = serverId,
            ConcurrencyToken = Guid.NewGuid().ToString("N"),
            Settings = settings,
            Secrets = secrets
        };
    }

    /// <summary>
    /// Used to create the <see cref="IReadOnlySettingCollectionProvider"/> for the OpenID server.
    /// </summary>
    protected internal virtual async ValueTask<IReadOnlySettingCollectionProvider> CreateSettingsProviderAsync(
        OpenIdEnvironment openIdEnvironment,
        PersistedServer persistedServer,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        var dataSources = new List<ICollectionDataSource<Setting>>(2);
        try
        {
            dataSources.Add(await CreateRootSettingsDataSourceAsync(propertyBag, cancellationToken));
            dataSources.Add(await CreateServerSettingsDataSourceAsync(
                    openIdEnvironment,
                    persistedServer,
                    propertyBag,
                    cancellationToken
                )
            );
            return SettingCollectionProviderFactory.Create(dataSources, owns: true);
        }
        catch
        {
            await dataSources.DisposeAllAsync();
            throw;
        }
    }

    /// <summary>
    /// Used to create a data source for the server's settings that are derived from the configurable options.
    /// </summary>
    protected internal virtual ValueTask<IDisposableCollectionDataSource<Setting>> CreateRootSettingsDataSourceAsync(
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var configurationSectionName = $"{Options.SectionName}:{OpenIdServerOptions.SettingsSubsection}";
        var configurationSection = Configuration.GetSection(configurationSectionName);

        var dataSource = ActivatorUtilities.CreateInstance<RootSettingsCollectionDataSource>(
            ServiceProvider,
            configurationSection
        );

        return ValueTask.FromResult<IDisposableCollectionDataSource<Setting>>(dataSource);
    }

    /// <summary>
    /// Used to create a data source for the server's settings that are persisted in a store.
    /// </summary>
    protected internal virtual ValueTask<IAsyncDisposableCollectionDataSource<Setting>> CreateServerSettingsDataSourceAsync(
        OpenIdEnvironment openIdEnvironment,
        PersistedServer persistedServer,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var jsonOptions = openIdEnvironment.JsonSerializerOptions;
        var initialSettings = SettingSerializer.DeserializeSettings(
            persistedServer.Settings.Value,
            jsonOptions
        );

        var dataSource = CollectionDataSourceFactory.CreatePeriodicPolling(
            new RefreshSettingsState(
                openIdEnvironment,
                persistedServer
            ),
            initialSettings,
            Options.Server.SettingsPeriodicRefreshInterval,
            RefreshSettingsAsync
        );

        return ValueTask.FromResult(dataSource);
    }

    private readonly record struct RefreshSettingsState(
        OpenIdEnvironment OpenIdEnvironment,
        PersistedServer PersistedServer
    );

    private async ValueTask<RefreshCollectionResult<Setting>> RefreshSettingsAsync(
        RefreshSettingsState state,
        IReadOnlyCollection<Setting> current,
        CancellationToken cancellationToken
    )
    {
        var (openIdEnvironment, persistedServer) = state;
        var serverId = persistedServer.ServerId;

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IServerStore>();

        var newSettings = await store.GetSettingsOrDefaultAsync(serverId, cancellationToken);
        if (newSettings is null)
            return RefreshCollectionResultFactory.Unchanged<Setting>();

        var prevSettings = persistedServer.Settings;
        var prevConcurrencyToken = prevSettings.ConcurrencyToken;
        var newConcurrencyToken = newSettings.ConcurrencyToken;

        if (string.Equals(prevConcurrencyToken, newConcurrencyToken, StringComparison.Ordinal))
            return RefreshCollectionResultFactory.Unchanged<Setting>();

        var jsonOptions = openIdEnvironment.JsonSerializerOptions;
        var settings = SettingSerializer.DeserializeSettings(newSettings.Value, jsonOptions);

        // update the state after successfully deserializing the settings
        persistedServer.Settings = newSettings;

        return RefreshCollectionResultFactory.Changed(settings);
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

        var initialSecrets = SecretSerializer.DeserializeSecrets(persistedServer.Secrets.Value, out _);

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

        var secrets = SecretSerializer.DeserializeSecrets(newSecrets.Value, out _);

        // update the state after successfully deserializing the secrets
        persistedServer.Secrets = newSecrets;

        return RefreshCollectionResultFactory.Changed(secrets);
    }
}
