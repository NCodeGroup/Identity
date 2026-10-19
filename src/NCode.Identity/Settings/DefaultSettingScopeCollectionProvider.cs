#region Copyright Preamble

// Copyright @ 2026 NCode Group
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
using NCode.Collections.Providers;

namespace NCode.Identity.Settings;

/// <summary>
/// Provides a default implementation of the <see cref="ISettingScopeCollectionProvider"/> abstraction.
/// </summary>
internal class DefaultSettingScopeCollectionProvider(
    ICollectionProviderFactory collectionProviderFactory,
    IEnumerable<ICollectionDataSource<SettingScopeDescriptor>> dataSources
) : ISettingScopeCollectionProvider
{
    private ICollectionProvider<SettingScopeDescriptor, ISettingScopeCollection> Inner { get; } =
        collectionProviderFactory.Create(
            items => new DefaultSettingScopeCollection(items),
            dataSources,
            owns: false
        );

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return Inner.DisposeAsync();
    }

    /// <inheritdoc />
    public ISettingScopeCollection Collection => Inner.Collection;

    /// <inheritdoc />
    public IChangeToken GetChangeToken() => Inner.GetChangeToken();
}
