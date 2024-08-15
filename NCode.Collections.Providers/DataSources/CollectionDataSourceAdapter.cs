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

using Microsoft.Extensions.Primitives;

namespace NCode.Collections.Providers.DataSources;

/// <remarks>
/// We are not disposable because the provider is responsible for managing the lifetime of the items.
/// </remarks>
internal class CollectionDataSourceAdapter<TItem>(
    ICollectionProvider<TItem, IEnumerable<TItem>> provider
) : ICollectionDataSource<TItem>
{
    private ICollectionProvider<TItem, IEnumerable<TItem>> Provider { get; } = provider;

    /// <inheritdoc />
    public IChangeToken GetChangeToken() => Provider.GetChangeToken();

    /// <inheritdoc />
    public IEnumerable<TItem> Collection => Provider.Collection;
}
