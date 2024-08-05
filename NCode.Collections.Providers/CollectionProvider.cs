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
/// Provides a default implementation for the <see cref="ICollectionProvider{TItem,TCollection}"/> abstraction.
/// </summary>
/// <typeparam name="TItem">The type of items in the collection.</typeparam>
/// <typeparam name="TCollection">The type of the collection.</typeparam>
[PublicAPI]
public class CollectionProvider<TItem, TCollection> : BaseCollectionProvider<TItem, TCollection>
    where TCollection : IEnumerable<TItem>
{
    private Func<IEnumerable<TItem>, TCollection> CollectionFactory { get; }

    /// <inheritdoc />
    public CollectionProvider(
        Func<IEnumerable<TItem>, TCollection> collectionFactory,
        ICollectionDataSource<TItem> dataSource,
        bool owns = true
    ) : base(dataSource, owns)
    {
        CollectionFactory = collectionFactory;
    }

    /// <inheritdoc />
    public CollectionProvider(
        Func<IEnumerable<TItem>, TCollection> collectionFactory,
        IEnumerable<ICollectionDataSource<TItem>> dataSources,
        bool owns = false
    ) : base(dataSources, owns)
    {
        CollectionFactory = collectionFactory;
    }

    /// <inheritdoc />
    protected override TCollection CreateCollection(IEnumerable<TItem> items)
    {
        return CollectionFactory(items);
    }
}
