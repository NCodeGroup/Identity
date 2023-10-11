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
using NCode.Jose.Extensions;
using NCode.Jose.Infrastructure;

namespace NCode.Jose.SecretKeys;

/// <summary>
/// Provides a default implementation for the <see cref="ISecretKeyCollection"/> interface.
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
    /// <see cref="SecretKey"/> instances. The collection will be sorted descending by the <see cref="KeyMetadata.ExpiresWhen"/>
    /// property.
    /// </summary>
    /// <param name="secretKeys">A collection of <see cref="SecretKey"/> instances.</param>
    /// <param name="owns">Indicates whether this new instance will own the <see cref="SecretKey"/> instances
    /// and dispose of them when this class is disposed. The default is <c>true</c>.</param>
    public SecretKeyCollection(IEnumerable<SecretKey> secretKeys, bool owns = true)
    {
        Owns = owns;
        SecretKeys = secretKeys
            .OrderByDescending(secretKey => secretKey.Metadata.ExpiresWhen ?? DateTimeOffset.MaxValue)
            .ToList();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SecretKeyCollection"/> class with the specified collection of
    /// <see cref="SecretKey"/> instances. The collection is used as-is and should already be sorted descending by the
    /// <see cref="KeyMetadata.ExpiresWhen"/> property.
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
