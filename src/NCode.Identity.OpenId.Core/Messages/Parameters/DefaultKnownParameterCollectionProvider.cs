// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Primitives;
using NCode.Collections.Providers;

namespace NCode.Identity.OpenId.Messages.Parameters;

/// <summary>
/// Provides a default implementation of the <see cref="IKnownParameterCollectionProvider"/> abstraction.
/// </summary>
internal class DefaultKnownParameterCollectionProvider(
    ICollectionProviderFactory collectionProviderFactory,
    IEnumerable<ICollectionDataSource<KnownParameter>> dataSources
) : IKnownParameterCollectionProvider
{
    private ICollectionProvider<KnownParameter, IKnownParameterCollection> Inner { get; } =
        collectionProviderFactory.Create(
            items => new DefaultKnownParameterCollection(items),
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
    public IKnownParameterCollection Collection => Inner.Collection;

    /// <inheritdoc />
    public IChangeToken GetChangeToken() => Inner.GetChangeToken();
}
