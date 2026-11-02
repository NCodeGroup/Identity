// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Collections.Providers;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Secrets.Collections;

/// <summary>
/// Factory abstraction for creating <see cref="ISecretKeyCollectionProvider"/> instances.
/// </summary>
[PublicAPI]
public interface ISecretKeyCollectionProviderFactory
{
    /// <summary>
    /// Factory method that creates a new <see cref="ISecretKeyCollectionProvider"/> instance
    /// with the specified <see cref="SecretKey"/> data source instance.
    /// This variant will own the data source and dispose of it when the provider itself is disposed.
    /// </summary>
    /// <param name="dataSource">The <see cref="SecretKey"/> data source instance.</param>
    /// <param name="owns">Indicates whether the provider will own the data source and dispose of it
    /// when the provider itself is disposed.</param>
    /// <returns>The newly created <see cref="ISecretKeyCollectionProvider"/> instance.</returns>
    ISecretKeyCollectionProvider Create(ICollectionDataSource<SecretKey> dataSource, bool owns);

    /// <summary>
    /// Factory method that creates a new <see cref="ISecretKeyCollectionProvider"/> instance
    /// with the specified <see cref="SecretKey"/> data source collection.
    /// </summary>
    /// <param name="dataSources">The collection of <see cref="SecretKey"/> data source instances.</param>
    /// <param name="owns">Indicates whether the provider will own the individual data sources and dispose of them
    /// when the class is disposed.</param>
    /// <returns>The newly created <see cref="ISecretKeyCollectionProvider"/> instance.</returns>
    ISecretKeyCollectionProvider Create(
        IEnumerable<ICollectionDataSource<SecretKey>> dataSources,
        bool owns
    );

    /// <summary>
    /// Factory method that creates a new <see cref="ISecretKeyCollectionProvider"/> instance using a static collection of
    /// <see cref="SecretKey"/> instances. The returned <see cref="ISecretKeyCollectionProvider"/> instance will not provide
    /// change notifications.
    /// </summary>
    /// <param name="secretKeys">The collection of <see cref="SecretKey"/> instances.</param>
    /// <returns>The newly created <see cref="ISecretKeyCollectionProvider"/> instance.</returns>
    ISecretKeyCollectionProvider CreateStatic(IEnumerable<SecretKey> secretKeys);
}
