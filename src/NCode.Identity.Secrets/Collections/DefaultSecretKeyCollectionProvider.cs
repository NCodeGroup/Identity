// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Collections.Providers;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Secrets.Collections;

/// <summary>
/// Provides a default implementation for the <see cref="ISecretKeyCollectionProvider"/> abstraction.
/// </summary>
internal class DefaultSecretKeyCollectionProvider
    : BaseCollectionProvider<SecretKey, ISecretKeyCollection>,
        ISecretKeyCollectionProvider
{
    private ISecretKeyCollectionFactory Factory { get; }

    /// <summary>
    /// Factory method to create a new instance of the <see cref="DefaultSecretKeyCollectionProvider"/> class.
    /// This variant will own the data source and dispose of it when the provider itself is disposed.
    /// </summary>
    public static DefaultSecretKeyCollectionProvider Create(
        ISecretKeyCollectionFactory factory,
        ICollectionDataSource<SecretKey> dataSource,
        bool owns
    ) => new(factory, dataSource, owns);

    /// <summary>
    /// Factory method to create a new instance of the <see cref="DefaultSecretKeyCollectionProvider"/> class.
    /// </summary>
    public static DefaultSecretKeyCollectionProvider Create(
        ISecretKeyCollectionFactory factory,
        IEnumerable<ICollectionDataSource<SecretKey>> dataSources,
        bool owns
    ) => new(factory, dataSources, owns);

    // private so that the other ctor is used for DI
    private DefaultSecretKeyCollectionProvider(
        ISecretKeyCollectionFactory factory,
        ICollectionDataSource<SecretKey> dataSource,
        bool owns
    )
        : base(dataSource, owns)
    {
        Factory = factory;
    }

    // private so that the other ctor is used for DI
    private DefaultSecretKeyCollectionProvider(
        ISecretKeyCollectionFactory factory,
        IEnumerable<ICollectionDataSource<SecretKey>> dataSources,
        bool owns
    )
        : base(dataSources, owns)
    {
        Factory = factory;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultSecretKeyCollectionProvider"/> class.
    /// This variant will not own the individual data sources and therefore not dispose of them when the provider itself
    /// is disposed because the data sources are resolved from the DI container which will manage their lifetimes.
    /// </summary>
    public DefaultSecretKeyCollectionProvider(
        ISecretKeyCollectionFactory factory,
        IEnumerable<ICollectionDataSource<SecretKey>> dataSources
    )
        : base(dataSources)
    {
        Factory = factory;
    }

    /// <inheritdoc />
    protected override ISecretKeyCollection CreateCollection(IEnumerable<SecretKey> items) =>
        Factory.Create(items);
}
