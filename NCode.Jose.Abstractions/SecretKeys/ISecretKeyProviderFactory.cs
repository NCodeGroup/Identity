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

namespace NCode.Jose.SecretKeys;

/// <summary>
/// Factory abstraction for creating new <see cref="ISecretKeyProvider"/> instances.
/// </summary>
public interface ISecretKeyProviderFactory
{
    /// <summary>
    /// Factory method that creates a new <see cref="ISecretKeyProvider"/> instance
    /// with the specified <see cref="SecretKey"/> data source collection.
    /// </summary>
    /// <param name="dataSources">The collection of <see cref="SecretKey"/> data source instances.</param>
    /// <returns>The newly created <see cref="ISecretKeyProvider"/> instance.</returns>
    ISecretKeyProvider Create(IEnumerable<ICollectionDataSource<SecretKey>> dataSources);

    /// <summary>
    /// Factory method that creates a new <see cref="ISecretKeyProvider"/> instance
    /// with the specified <see cref="SecretKey"/> data source collection.
    /// </summary>
    /// <param name="dataSources">The collection of <see cref="SecretKey"/> data source instances.</param>
    /// <returns>The newly created <see cref="ISecretKeyProvider"/> instance.</returns>
    ISecretKeyProvider Create(params ICollectionDataSource<SecretKey>[] dataSources);

    /// <summary>
    /// Factory method that creates a new <see cref="ISecretKeyProvider"/> instance using a static collection of
    /// <see cref="SecretKey"/> instances. The returned <see cref="ISecretKeyProvider"/> instance will not provide
    /// change notifications.
    /// </summary>
    /// <param name="secretKeys">The collection of <see cref="SecretKey"/> instances.</param>
    /// <returns>The newly created <see cref="ISecretKeyProvider"/> instance.</returns>
    ISecretKeyProvider CreateStatic(IEnumerable<SecretKey> secretKeys);
}
