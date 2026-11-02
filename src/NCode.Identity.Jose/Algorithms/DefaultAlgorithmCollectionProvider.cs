// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Collections.Providers;

namespace NCode.Identity.Jose.Algorithms;

/// <summary>
/// Provides a default implementation for the <see cref="IAlgorithmCollectionProvider"/> interface.
/// </summary>
internal class DefaultAlgorithmCollectionProvider(
    IEnumerable<ICollectionDataSource<Algorithm>> dataSources
)
    : BaseCollectionProvider<Algorithm, IAlgorithmCollection>(dataSources),
        IAlgorithmCollectionProvider
{
    /// <inheritdoc />
    protected override IAlgorithmCollection CreateCollection(IEnumerable<Algorithm> items) =>
        new DefaultAlgorithmCollection(items);
}
