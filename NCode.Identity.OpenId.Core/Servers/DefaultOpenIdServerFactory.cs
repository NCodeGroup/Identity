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

using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NCode.Collections.Providers;
using NCode.Collections.Providers.PeriodicPolling;
using NCode.Disposables;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Options;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Persistence.DataContracts;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Persistence.Logic;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Servers;

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
    ISecretKeyCollectionProviderFactory secretKeyCollectionProviderFactory
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

    /// <inheritdoc />
    public async ValueTask<OpenIdServer> CreateAsync(
        OpenIdEnvironment openIdEnvironment,
        CancellationToken cancellationToken
    )
    {
        var disposables = new List<object>(2);
        var propertyBag = PropertyBagFactory.Create();
        var persistedServer = await GetPersistedServerAsync(propertyBag, cancellationToken);
        try
        {
            var settingsProvider = await CreateSettingsProviderAsync(openIdEnvironment, persistedServer, propertyBag, cancellationToken);
            disposables.Add(settingsProvider);

            var secretsProvider = await CreateSecretsProviderAsync(persistedServer, propertyBag, cancellationToken);
            disposables.Add(secretsProvider);

            return Create(settingsProvider, secretsProvider, propertyBag);
        }
        catch
        {
            await disposables.DisposeAllAsync();
            throw;
        }
    }

    protected internal virtual OpenIdServer Create(
        IReadOnlySettingCollectionProvider settingsProvider,
        ISecretKeyCollectionProvider secretsProvider,
        IPropertyBag propertyBag
    ) => new DefaultOpenIdServer(
        settingsProvider,
        secretsProvider,
        propertyBag
    );

    protected internal virtual async ValueTask<PersistedServer> GetPersistedServerAsync(
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IServerStore>();
        return await store.GetAsync(cancellationToken);
    }

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

    protected internal virtual ValueTask<IAsyncDisposableCollectionDataSource<Setting>> CreateServerSettingsDataSourceAsync(
        OpenIdEnvironment openIdEnvironment,
        PersistedServer persistedServer,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var initialSettings = SettingSerializer.DeserializeSettings(
            openIdEnvironment,
            persistedServer.SettingsState.Value
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

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IServerStore>();

        var prevSettingsState = persistedServer.SettingsState;
        var (newSettingsJson, newConcurrencyToken) = await store.GetSettingsAsync(prevSettingsState, cancellationToken);

        var prevConcurrencyToken = prevSettingsState.ConcurrencyToken;
        if (string.Equals(prevConcurrencyToken, newConcurrencyToken, StringComparison.Ordinal))
            return RefreshCollectionResultFactory.Unchanged<Setting>();

        var newSettings = SettingSerializer.DeserializeSettings(openIdEnvironment, newSettingsJson);

        // update the state after successfully deserializing the settings
        persistedServer.SettingsState = ConcurrentStateFactory.Create(newSettingsJson, newConcurrencyToken);

        return RefreshCollectionResultFactory.Changed(newSettings);
    }

    protected internal virtual ValueTask<ISecretKeyCollectionProvider> CreateSecretsProviderAsync(
        PersistedServer persistedServer,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var initialSecrets = SecretSerializer.DeserializeSecrets(persistedServer.SecretsState.Value, out _);

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
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IServerStore>();

        var prevPersistedSecrets = persistedServer.SecretsState;
        var (newPersistedSecrets, newConcurrencyToken) = await store.GetSecretsAsync(prevPersistedSecrets, cancellationToken);

        var prevConcurrencyToken = prevPersistedSecrets.ConcurrencyToken;
        if (string.Equals(prevConcurrencyToken, newConcurrencyToken, StringComparison.Ordinal))
            return RefreshCollectionResultFactory.Unchanged<SecretKey>();

        var newSecrets = SecretSerializer.DeserializeSecrets(newPersistedSecrets, out _);

        // update the state after successfully deserializing the secrets
        persistedServer.SecretsState = ConcurrentStateFactory.Create(newPersistedSecrets, newConcurrencyToken);

        return RefreshCollectionResultFactory.Changed(newSecrets);
    }
}
