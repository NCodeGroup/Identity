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

namespace NIdentity.OpenId.Cryptography.Keys;

/// <summary>
/// Provides a collection of <see cref="SecretKey"/> instances that are disposed when the collection itself is disposed.
/// </summary>
public interface ISecretKeyCollection : IReadOnlyCollection<SecretKey>, IDisposable
{
    /// <summary>
    /// Attempts to get a <see cref="SecretKey"/> with the specified <c>Key ID (KID)</c>.
    /// </summary>
    /// <param name="keyId">The <c>Key ID (KID)</c> of the <see cref="SecretKey"/> to get.</param>
    /// <param name="secretKey">When this method returns, the <see cref="SecretKey"/> with the specified <c>Key ID (KID)</c>,
    /// if found; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if a <see cref="SecretKey"/> was found with the specified <c>Key ID (KID)</c>; otherwise, <c>false</c>.</returns>
    bool TryGet(string keyId, [MaybeNullWhen(false)] out SecretKey secretKey);
}

/// <summary>
/// Provides a default implementation for <see cref="ISecretKeyCollection"/>.
/// </summary>
public class SecretKeyCollection : ISecretKeyCollection
{
    private bool IsDisposed { get; set; }

    private IDictionary<string, SecretKey> SecretKeysByKeyId { get; }

    /// <inheritdoc />
    public int Count => SecretKeysByKeyId.Count;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecretKeyCollection"/> class.
    /// </summary>
    /// <param name="collection">A collection of <see cref="SecretKey"/> instances.</param>
    public SecretKeyCollection(IEnumerable<SecretKey> collection)
    {
        SecretKeysByKeyId = collection.ToDictionary(item => item.KeyId, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// When overridden in a derived class, releases the unmanaged resources used by this instance,
    /// and optionally releases any managed resources.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c>
    /// to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing || IsDisposed) return;
        IsDisposed = true;

        foreach (var secretKey in SecretKeysByKeyId.Values)
        {
            secretKey.Dispose();
        }
    }

    /// <inheritdoc />
    public bool TryGet(string keyId, [MaybeNullWhen(false)] out SecretKey secretKey) =>
        SecretKeysByKeyId.TryGetValue(keyId, out secretKey);

    /// <inheritdoc />
    public IEnumerator<SecretKey> GetEnumerator() =>
        SecretKeysByKeyId.Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() =>
        GetEnumerator();
}
