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

using NCode.Collections.Providers;
using NCode.Collections.Providers.PeriodicPolling;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Provides a default implementation of the <see cref="ISettingCollectionProviderFactory"/> abstraction.
/// </summary>
public class DefaultSettingCollectionProviderFactory(
    ISettingDescriptorCollectionProvider settingDescriptorCollectionProvider,
    ICollectionDataSourceFactory collectionDataSourceFactory
) : ISettingCollectionProviderFactory
{
    private ISettingDescriptorCollectionProvider SettingDescriptorCollectionProvider { get; } = settingDescriptorCollectionProvider;
    private ICollectionDataSourceFactory CollectionDataSourceFactory { get; } = collectionDataSourceFactory;

    /// <inheritdoc />
    public async ValueTask<ISettingCollectionProvider> CreateProviderAsync<TState>(
        ISettingCollectionProvider? parentSettings,
        TState state,
        TimeSpan refreshInterval,
        LoadSettingsAsyncDelegate<TState> loadSettingsAsync,
        CancellationToken cancellationToken)
    {
        var initialSettings = await loadSettingsAsync(state, cancellationToken);

        var dataSource = CollectionDataSourceFactory.CreatePeriodicPolling(
            (state, loadSettingsAsync),
            initialSettings,
            refreshInterval,
            LoadSettingsAsync);

        return new SettingCollectionProvider(
            SettingDescriptorCollectionProvider,
            parentSettings,
            dataSource);
    }

    private static async ValueTask<IReadOnlyCollection<Setting>?> LoadSettingsAsync<TState>(
        PeriodicPollingCollectionContext<Setting, (TState state, LoadSettingsAsyncDelegate<TState> loadSettingsAsync)> context,
        CancellationToken cancellationToken)
    {
        var (state, loadSettingsAsync) = context.State;
        return await loadSettingsAsync(state, cancellationToken);
    }
}

// private async ValueTask<IReadOnlyCollection<Setting>> LoadSettingsAsync(CancellationToken cancellationToken)
// {
//     await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
//
//     var serverStore = storeManager.GetStore<IServerStore>();
//     var settingsJson = await serverStore.GetSettingsAsync(cancellationToken);
//
//     var settings = settingsJson.Deserialize<IReadOnlyCollection<Setting>>() ??
//                    throw new InvalidOperationException("Failed to deserialize settings.");
//
//     return settings;
// }
