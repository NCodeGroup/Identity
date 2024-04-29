#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using NCode.Collections.Providers;

namespace NCode.Identity.Secrets;

/// <summary>
/// Provides a default implementation for the <see cref="ISecretKeyProvider"/> abstraction.
/// </summary>
public class DefaultSecretKeyProvider : CollectionProvider<SecretKey, ISecretKeyCollection>, ISecretKeyProvider
{
    private ISecretKeyCollectionFactory Factory { get; }

    /// <summary>
    /// Factory method to create a new instance of the <see cref="DefaultSecretKeyProvider"/> class.
    /// This variant will own the data source and dispose of it when the provider itself is disposed.
    /// </summary>
    public static DefaultSecretKeyProvider Create(
        ISecretKeyCollectionFactory factory,
        ICollectionDataSource<SecretKey> dataSource
    ) => new(factory, dataSource);

    /// <summary>
    /// Factory method to create a new instance of the <see cref="DefaultSecretKeyProvider"/> class.
    /// </summary>
    public static DefaultSecretKeyProvider Create(
        ISecretKeyCollectionFactory factory,
        IEnumerable<ICollectionDataSource<SecretKey>> dataSources,
        bool owns = false
    ) => new(factory, dataSources, owns);

    // private so that the other ctor is used for DI
    private DefaultSecretKeyProvider(
        ISecretKeyCollectionFactory factory,
        ICollectionDataSource<SecretKey> dataSource
    ) : base(dataSource)
    {
        Factory = factory;
    }

    // private so that the other ctor is used for DI
    private DefaultSecretKeyProvider(
        ISecretKeyCollectionFactory factory,
        IEnumerable<ICollectionDataSource<SecretKey>> dataSources,
        bool owns
    ) : base(dataSources, owns)
    {
        Factory = factory;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultSecretKeyProvider"/> class.
    /// </summary>
    public DefaultSecretKeyProvider(
        ISecretKeyCollectionFactory factory,
        IEnumerable<ICollectionDataSource<SecretKey>> dataSources
    ) : base(dataSources)
    {
        Factory = factory;
    }

    /// <inheritdoc />
    protected override ISecretKeyCollection CreateCollection(IEnumerable<SecretKey> items) =>
        Factory.Create(items);
}
