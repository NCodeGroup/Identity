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

using NCode.Jose.Collections;

namespace NCode.Jose.SecretKeys;

/// <summary>
/// Provides a default implementation for the <see cref="ISecretKeyProvider"/> interface.
/// </summary>
public class SecretKeyProvider : CollectionProvider<SecretKey, ISecretKeyCollection>, ISecretKeyProvider
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SecretKeyProvider"/> class with the specified collection of <see cref="ICollectionDataSource{SecretKey}"/> instances.
    /// </summary>
    /// <param name="dataSources">A collection of <see cref="ICollectionDataSource{SecretKey}"/> instances to aggregate.</param>
    public SecretKeyProvider(IEnumerable<ICollectionDataSource<SecretKey>> dataSources)
        : base(dataSources)
    {
        // nothing
    }

    /// <inheritdoc />
    protected override ISecretKeyCollection CreateCollection(IEnumerable<SecretKey> items)
    {
        return new SecretKeyCollection(items);
    }
}
