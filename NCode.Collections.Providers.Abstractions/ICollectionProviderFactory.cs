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

namespace NCode.Collections.Providers;

/// <summary>
/// Provides the ability to create instances of <see cref="ICollectionProvider{TItem,TCollection}"/>.
/// </summary>
[PublicAPI]
public interface ICollectionProviderFactory
{
    /// <summary>
    /// Creates a new instance of <see cref="ICollectionProvider{TItem,TCollection}"/> using a single data source.
    /// </summary>
    /// <param name="collectionFactory">The factory method to create the collection.</param>
    /// <param name="dataSource">The data source to use to populate the collection.</param>
    /// <param name="owns">Specifies if the collection provider should own the data source.</param>
    /// <typeparam name="TItem">The type of items in the collection.</typeparam>
    /// <typeparam name="TCollection">The type of collection.</typeparam>
    /// <returns>The new created collection provider instance.</returns>
    ICollectionProvider<TItem, TCollection> Create<TItem, TCollection>(
        Func<IEnumerable<TItem>, TCollection> collectionFactory,
        ICollectionDataSource<TItem> dataSource,
        bool owns = true
    ) where TCollection : IEnumerable<TItem>;

    /// <summary>
    /// Creates a new instance of <see cref="ICollectionProvider{TItem,TCollection}"/> using multiple data sources.
    /// </summary>
    /// <param name="collectionFactory">The factory method to create the collection.</param>
    /// <param name="dataSources">The collection of data sources to use to populate the collection.</param>
    /// <param name="owns">Specifies if the collection provider should own the data sources.</param>
    /// <typeparam name="TItem">The type of items in the collection.</typeparam>
    /// <typeparam name="TCollection">The type of collection.</typeparam>
    /// <returns>The new created collection provider instance.</returns>
    ICollectionProvider<TItem, TCollection> Create<TItem, TCollection>(
        Func<IEnumerable<TItem>, TCollection> collectionFactory,
        IEnumerable<ICollectionDataSource<TItem>> dataSources,
        bool owns = false
    ) where TCollection : IEnumerable<TItem>;
}
