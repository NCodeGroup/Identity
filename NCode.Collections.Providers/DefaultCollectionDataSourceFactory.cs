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

using System.Collections.ObjectModel;
using NCode.Collections.Providers.DataSources;
using NCode.Collections.Providers.PeriodicPolling;

namespace NCode.Collections.Providers;

/// <summary>
/// Provides a default implementation of the <see cref="ICollectionDataSourceFactory"/> abstraction.
/// </summary>
public class DefaultCollectionDataSourceFactory : ICollectionDataSourceFactory
{
    /// <inheritdoc />
    public ICollectionDataSource<T> CreateComposite<T>(
        IEnumerable<ICollectionDataSource<T>> dataSources,
        bool owns = false)
    {
        return new CompositeCollectionDataSource<T>(dataSources)
        {
            Owns = owns
        };
    }

    /// <inheritdoc />
    public ICollectionDataSource<T> CreateStatic<T>(
        IEnumerable<T> collection,
        bool owns = true)
    {
        return new StaticCollectionDataSource<T>(
            collection,
            owns);
    }

    /// <inheritdoc />
    public ICollectionDataSource<T> CreateObservable<T>(
        ObservableCollection<T> observableCollection,
        bool owns = true)
    {
        return new ObservableCollectionDataSource<T>(
            observableCollection,
            owns);
    }

    /// <inheritdoc />
    public ICollectionDataSource<TItem> CreatePeriodicPolling<TItem, TState>(
        TState state,
        IReadOnlyCollection<TItem> initialCollection,
        TimeSpan refreshInterval,
        RefreshCollectionAsyncDelegate<TItem, TState> refreshCollectionAsync,
        HandleExceptionAsyncDelegate? handleExceptionAsync = default)
    {
        return new PeriodicPollingCollectionDataSource<TItem, TState>(
            state,
            initialCollection,
            refreshInterval,
            refreshCollectionAsync,
            handleExceptionAsync);
    }
}
