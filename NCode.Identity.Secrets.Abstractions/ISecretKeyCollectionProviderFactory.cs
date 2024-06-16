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
/// Factory abstraction for creating <see cref="ISecretKeyCollectionProvider"/> instances.
/// </summary>
public interface ISecretKeyCollectionProviderFactory
{
    /// <summary>
    /// Factory method that creates a new <see cref="ISecretKeyCollectionProvider"/> instance
    /// with the specified <see cref="SecretKey"/> data source instance.
    /// This variant will own the data source and dispose of it when the provider itself is disposed.
    /// </summary>
    /// <param name="dataSource">The <see cref="SecretKey"/> data source instance.</param>
    /// <param name="owns">Indicates whether the provider will own the data source and dispose of it
    /// when the provider itself is disposed. The default is <c>true</c>.</param>
    /// <returns>The newly created <see cref="ISecretKeyCollectionProvider"/> instance.</returns>
    ISecretKeyCollectionProvider Create(ICollectionDataSource<SecretKey> dataSource, bool owns = true);

    /// <summary>
    /// Factory method that creates a new <see cref="ISecretKeyCollectionProvider"/> instance
    /// with the specified <see cref="SecretKey"/> data source collection.
    /// </summary>
    /// <param name="dataSources">The collection of <see cref="SecretKey"/> data source instances.</param>
    /// <param name="owns">Indicates whether the provider will own the individual data sources and dispose of them
    /// when the class is disposed. The default is <c>false</c>.</param>
    /// <returns>The newly created <see cref="ISecretKeyCollectionProvider"/> instance.</returns>
    ISecretKeyCollectionProvider Create(IEnumerable<ICollectionDataSource<SecretKey>> dataSources, bool owns = false);

    /// <summary>
    /// Factory method that creates a new <see cref="ISecretKeyCollectionProvider"/> instance using a static collection of
    /// <see cref="SecretKey"/> instances. The returned <see cref="ISecretKeyCollectionProvider"/> instance will not provide
    /// change notifications.
    /// </summary>
    /// <param name="secretKeys">The collection of <see cref="SecretKey"/> instances.</param>
    /// <returns>The newly created <see cref="ISecretKeyCollectionProvider"/> instance.</returns>
    ISecretKeyCollectionProvider CreateStatic(IEnumerable<SecretKey> secretKeys);
}
