// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Collections.Providers;

namespace NCode.Identity.Settings;

/// <summary>
/// Factory abstraction for creating <see cref="IReadOnlySettingCollectionProvider"/> instances.
/// </summary>
[PublicAPI]
public interface IReadOnlySettingCollectionProviderFactory
{
    /// <summary>
    /// Creates a new instance of <see cref="IReadOnlySettingCollectionProvider"/> using multiple data sources.
    /// </summary>
    /// <param name="dataSources">The collection of data sources to use to populate the collection.</param>
    /// <param name="owns">Specifies if the collection provider should own the data sources.</param>
    /// <returns>The new created collection provider instance.</returns>
    IReadOnlySettingCollectionProvider Create(
        IEnumerable<ICollectionDataSource<Setting>> dataSources,
        bool owns = false
    );
}
