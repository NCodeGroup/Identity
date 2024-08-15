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
using NCode.Collections.Providers.DataSources;

namespace NCode.Collections.Providers;

/// <summary>
/// Provides extension methods for the <see cref="ICollectionProvider{TItem,TCollection}"/> abstraction.
/// </summary>
[PublicAPI]
public static class CollectionProviderExtensions
{
    /// <summary>
    /// Adapts an <see cref="ICollectionProvider{TItem,TCollection}"/> to an <see cref="ICollectionDataSource{TItem}"/>.
    /// The newly created <see cref="ICollectionDataSource{TItem}"/> will not be disposable since the original
    /// <see cref="ICollectionProvider{TItem,TCollection}"/> controls the lifetime of the items.
    /// </summary>
    public static ICollectionDataSource<TItem> AsDataSource<TItem>(this ICollectionProvider<TItem, IEnumerable<TItem>> provider)
    {
        return new CollectionDataSourceAdapter<TItem>(provider);
    }
}
