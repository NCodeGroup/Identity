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

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using NCode.Cryptography.Keys.Internal;

namespace NCode.Cryptography.Keys.DataSources;

/// <summary>
/// Provides an implementation of <see cref="ISecretKeyCollection"/> that aggregates multiple <see cref="ISecretKeyCollection"/> instances.
/// </summary>
public class CompositeSecretKeyCollection : BaseDisposable, ISecretKeyCollection
{
    private bool Owns { get; }
    private IEnumerable<ISecretKeyCollection> Collections { get; }
    private IEnumerable<ISecretKeyCollection> CollectionsOrThrowObjectDisposed => GetOrThrowObjectDisposed(Collections);

    /// <inheritdoc />
    public int Count => CollectionsOrThrowObjectDisposed.Sum(collection => collection.Count);

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeSecretKeyCollection"/> class with the specified collection of
    /// <see cref="ISecretKeyCollection"/> instances.
    /// </summary>
    /// <param name="collections">A collection of <see cref="ISecretKeyCollection"/> instances to aggregate.</param>
    /// <param name="owns">Indicates whether the new collection will own the <see cref="ISecretKeyCollection"/> instances
    /// and dispose them when done. The default is <c>false</c>.</param>
    public CompositeSecretKeyCollection(IEnumerable<ISecretKeyCollection> collections, bool owns = false)
    {
        Owns = owns;
        Collections = collections;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (!disposing || IsDisposed) return;
        IsDisposed = true;

        if (!Owns) return;
        Collections.DisposeAll();
    }

    /// <inheritdoc />
    public bool TryGetByKeyId(string keyId, [MaybeNullWhen(false)] out SecretKey secretKey)
    {
        foreach (var collection in CollectionsOrThrowObjectDisposed)
        {
            if (collection.TryGetByKeyId(keyId, out secretKey)) return true;
        }

        secretKey = null;
        return false;
    }

    /// <inheritdoc />
    public IEnumerator<SecretKey> GetEnumerator() =>
        CollectionsOrThrowObjectDisposed.SelectMany(collection => collection).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() =>
        GetEnumerator();
}
