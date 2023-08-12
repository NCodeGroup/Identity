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

using NCode.Cryptography.Keys.DataSources;

namespace NCode.Cryptography.Keys;

/// <summary>
/// Provides the root (i.e. top-level) collection of <see cref="SecretKey"/> instances by aggregating multiple <see cref="ISecretKeyDataSource"/> instances.
/// </summary>
public interface ISecretKeyProvider : ISecretKeyDataSource
{
    // nothing
}

/// <summary>
/// Provides a default implementation for <see cref="ISecretKeyProvider"/>.
/// </summary>
public class SecretKeyProvider : CompositeSecretKeyDataSource, ISecretKeyProvider
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SecretKeyProvider"/> class with the specified collection of <see cref="ISecretKeyDataSource"/> instances.
    /// </summary>
    /// <param name="dataSources">A collection of <see cref="ISecretKeyDataSource"/> instances to aggregate.</param>
    public SecretKeyProvider(IEnumerable<ISecretKeyDataSource> dataSources)
        : base(dataSources)
    {
        // nothing
    }
}
