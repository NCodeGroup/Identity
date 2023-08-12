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

namespace NCode.Cryptography.Keys;

/// <summary>
/// Provides a read-only collection of <see cref="SecretKey"/> instances that can be accessed by <c>Key ID (KID)</c>.
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
    bool TryGetByKeyId(string keyId, [MaybeNullWhen(false)] out SecretKey secretKey);
}

/// <summary>
/// Provides a default implementation for <see cref="ISecretKeyCollection"/>.
/// </summary>
public class SecretKeyCollection : BaseDisposable, ISecretKeyCollection
{
    private bool Owns { get; }
    private IReadOnlyCollection<SecretKey> SecretKeys { get; }
    private IReadOnlyDictionary<string, SecretKey>? SecretKeysByKeyIdOrNull { get; set; }
    private IReadOnlyDictionary<string, SecretKey> SecretKeysByKeyId => SecretKeysByKeyIdOrNull ??= LoadSecretKeysByKeyId();

    /// <inheritdoc />
    public int Count => GetOrThrowObjectDisposed(SecretKeys).Count;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecretKeyCollection"/> class with the specified collection of
    /// <see cref="SecretKey"/> instances.
    /// </summary>
    /// <param name="secretKeys">A collection of <see cref="SecretKey"/> instances.</param>
    /// <param name="owns">Indicates whether the new collection will own the <see cref="SecretKey"/> instances
    /// and dispose them when done. The default is <c>true</c>.</param>
    public SecretKeyCollection(IEnumerable<SecretKey> secretKeys, bool owns = true)
    {
        Owns = owns;
        SecretKeys = secretKeys.ToList();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SecretKeyCollection"/> class with the specified collection of
    /// <see cref="SecretKey"/> instances.
    /// </summary>
    /// <param name="secretKeys">A collection of <see cref="SecretKey"/> instances.</param>
    /// <param name="owns">Indicates whether the new collection will own the <see cref="SecretKey"/> instances
    /// and dispose them when done. The default is <c>true</c>.</param>
    public SecretKeyCollection(IReadOnlyCollection<SecretKey> secretKeys, bool owns = true)
    {
        Owns = owns;
        SecretKeys = secretKeys;
    }

    /// <summary>
    /// When overridden in a derived class, releases the unmanaged resources used by this instance,
    /// and optionally releases any managed resources.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c>
    /// to release only unmanaged resources.</param>
    protected override void Dispose(bool disposing)
    {
        if (!disposing || IsDisposed) return;
        IsDisposed = true;

        if (!Owns) return;
        SecretKeys.DisposeAll();
    }

    private IReadOnlyDictionary<string, SecretKey> LoadSecretKeysByKeyId()
    {
        var dictionary = new Dictionary<string, SecretKey>(StringComparer.Ordinal);

        foreach (var secretKey in SecretKeys)
        {
            var keyId = secretKey.KeyId;
            if (string.IsNullOrEmpty(keyId)) continue;
            dictionary.TryAdd(keyId, secretKey);
        }

        return dictionary;
    }

    /// <inheritdoc />
    public bool TryGetByKeyId(string keyId, [MaybeNullWhen(false)] out SecretKey secretKey) =>
        GetOrThrowObjectDisposed(SecretKeysByKeyId).TryGetValue(keyId, out secretKey);

    /// <inheritdoc />
    public IEnumerator<SecretKey> GetEnumerator() =>
        GetOrThrowObjectDisposed(SecretKeys).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() =>
        GetEnumerator();
}
