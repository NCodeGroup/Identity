// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Primitives;
using NCode.Collections.Providers;

namespace NCode.Identity.Settings;

/// <summary>
/// Provides a default implementation of the <see cref="ISettingDescriptorCollectionProvider"/> abstraction.
/// </summary>
internal class DefaultSettingDescriptorCollectionProvider(
    ICollectionProviderFactory collectionProviderFactory,
    IEnumerable<ICollectionDataSource<SettingDescriptor>> dataSources
) : ISettingDescriptorCollectionProvider
{
    private ICollectionProvider<SettingDescriptor, ISettingDescriptorCollection> Inner { get; } =
        collectionProviderFactory.Create(
            items => new DefaultSettingDescriptorCollection(items),
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
    public ISettingDescriptorCollection Collection => Inner.Collection;

    /// <inheritdoc />
    public IChangeToken GetChangeToken() => Inner.GetChangeToken();
}
