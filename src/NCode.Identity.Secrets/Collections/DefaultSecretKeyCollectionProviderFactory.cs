// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Collections.Providers;
using NCode.Collections.Providers.DataSources;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Secrets.Collections;

/// <summary>
/// Provides a default implementation of the <see cref="ISecretKeyCollectionProviderFactory"/> abstraction.
/// </summary>
internal class DefaultSecretKeyCollectionProviderFactory(
    ISecretKeyCollectionFactory secretKeyCollectionFactory
) : ISecretKeyCollectionProviderFactory
{
    private ISecretKeyCollectionFactory SecretKeyCollectionFactory { get; } =
        secretKeyCollectionFactory;

    /// <inheritdoc />
    public ISecretKeyCollectionProvider Create(
        ICollectionDataSource<SecretKey> dataSource,
        bool owns
    ) => DefaultSecretKeyCollectionProvider.Create(SecretKeyCollectionFactory, dataSource, owns);

    /// <inheritdoc />
    public ISecretKeyCollectionProvider Create(
        IEnumerable<ICollectionDataSource<SecretKey>> dataSources,
        bool owns
    ) => DefaultSecretKeyCollectionProvider.Create(SecretKeyCollectionFactory, dataSources, owns);

    /// <inheritdoc />
    public ISecretKeyCollectionProvider CreateStatic(IEnumerable<SecretKey> secretKeys) =>
        Create(new StaticCollectionDataSource<SecretKey>(secretKeys), owns: true);
}
