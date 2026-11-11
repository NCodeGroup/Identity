// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
