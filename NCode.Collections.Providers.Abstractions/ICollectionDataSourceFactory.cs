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
using JetBrains.Annotations;
using NCode.Collections.Providers.PeriodicPolling;

namespace NCode.Collections.Providers;

/// <summary>
/// Provides the ability to create instances of <see cref="ICollectionDataSource{T}"/>.
/// </summary>
[PublicAPI]
public interface ICollectionDataSourceFactory
{
    /// <summary>
    /// Creates a single data source instance from the provided collection of data source instances.
    /// </summary>
    /// <param name="dataSources">The collection of data source instances to combine.</param>
    /// <param name="owns">Specifies whether the created data source instance should own the provided data source instances.</param>
    /// <typeparam name="T">The type of items in the collection.</typeparam>
    /// <returns>The newly created data source instance.</returns>
    ICollectionDataSource<T> CreateComposite<T>(
        IEnumerable<ICollectionDataSource<T>> dataSources,
        bool owns = false);

    /// <summary>
    /// Creates a data source instance that wraps the provided collection of items and does not provide any change notifications.
    /// </summary>
    /// <param name="collection">The collection of items to wrap.</param>
    /// <param name="owns">Specifies whether the created data source instance should own the provided collection of items.</param>
    /// <typeparam name="T">The type of items in the collection.</typeparam>
    /// <returns>The newly created data source instance.</returns>
    ICollectionDataSource<T> CreateStatic<T>(
        IEnumerable<T> collection,
        bool owns = true);

    /// <summary>
    /// Creates a data source instance that wraps the provided observable collection of items and provides change notifications.
    /// </summary>
    /// <param name="observableCollection">The observable collection of items to wrap.</param>
    /// <param name="owns">Specifies whether the created data source instance should own the provided observable collection of items.</param>
    /// <typeparam name="T">The type of items in the collection.</typeparam>
    /// <returns>The newly created data source instance.</returns>
    ICollectionDataSource<T> CreateObservable<T>(
        ObservableCollection<T> observableCollection,
        bool owns = true);

    /// <summary>
    /// Creates a data source instance that periodically polls for updates from a remote source.
    /// </summary>
    /// <param name="state">The state instance to pass to the refresh method when polling for updates.</param>
    /// <param name="initialCollection">The initial collection of items to use when the data source is first created.</param>
    /// <param name="refreshInterval">The interval at which to poll for updates.</param>
    /// <param name="getCollectionAsync">The delegate to call to refresh the collection of items.</param>
    /// <param name="handleExceptionAsync">The delegate to call to handle exceptions that occur during the refresh of the collection.</param>
    /// <typeparam name="TItem">The type of items in the collection.</typeparam>
    /// <typeparam name="TState">The type of the state parameter used during refresh calls.</typeparam>
    /// <returns>The newly created data source instance.</returns>
    ICollectionDataSource<TItem> CreatePeriodicPolling<TItem, TState>(
        TState state,
        IReadOnlyCollection<TItem> initialCollection,
        TimeSpan refreshInterval,
        GetCollectionAsyncDelegate<TItem, TState> getCollectionAsync,
        HandleExceptionAsyncDelegate? handleExceptionAsync = default);
}
