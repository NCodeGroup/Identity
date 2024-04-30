#region Copyright Preamble

// Copyright @ 2024 NCode Group
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
/// Provides a default implementation of the <see cref="ISecretKeyProviderFactory"/> abstraction.
/// </summary>
public class DefaultSecretKeyProviderFactory(
    ISecretKeyCollectionFactory secretKeyCollectionFactory
) : ISecretKeyProviderFactory
{
    private ISecretKeyCollectionFactory SecretKeyCollectionFactory { get; } = secretKeyCollectionFactory;

    /// <inheritdoc />
    public ISecretKeyProvider Create(ICollectionDataSource<SecretKey> dataSource, bool owns) =>
        DefaultSecretKeyProvider.Create(SecretKeyCollectionFactory, dataSource, owns);

    /// <inheritdoc />
    public ISecretKeyProvider Create(IEnumerable<ICollectionDataSource<SecretKey>> dataSources, bool owns) =>
        DefaultSecretKeyProvider.Create(SecretKeyCollectionFactory, dataSources, owns);

    /// <inheritdoc />
    public ISecretKeyProvider CreateStatic(IEnumerable<SecretKey> secretKeys) =>
        Create(new StaticCollectionDataSource<SecretKey>(secretKeys), owns: true);
}
