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

using Microsoft.Extensions.Primitives;
using NCode.Cryptography.Keys.Internal;

namespace NCode.Cryptography.Keys.DataSources;

/// <summary>
/// Provides an implementation of <see cref="ISecretKeyDataSource"/> that returns a static collection of <see cref="SecretKey"/> instances.
/// </summary>
public class StaticSecretKeyDataSource : SecretKeyDataSource
{
    private IEnumerable<SecretKey> Collection { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="StaticSecretKeyDataSource"/> class with the specified collection of <see cref="SecretKey"/> instances.
    /// </summary>
    /// <param name="collection">A collection of <see cref="SecretKey"/> instances.</param>
    public StaticSecretKeyDataSource(IEnumerable<SecretKey> collection)
    {
        Collection = new SecretKeyCollection(collection);
    }

    /// <inheritdoc />
    public override IEnumerable<SecretKey> SecretKeys => GetOrThrowObjectDisposed(Collection);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (!disposing || IsDisposed) return;
        IsDisposed = true;
        Collection.DisposeAll();
    }

    /// <inheritdoc />
    public override IChangeToken GetChangeToken() => GetOrThrowObjectDisposed(NullChangeToken.Singleton);
}
