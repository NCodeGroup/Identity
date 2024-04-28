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

namespace NCode.Identity.Secrets;

/// <summary>
/// Provides a default implementation for the <see cref="ISecretKeyCollection"/> interface.
/// </summary>
public class DefaultSecretKeyCollection : ISecretKeyCollection
{
    private List<SecretKey> SecretKeys { get; }
    private Dictionary<string, SecretKey>? SecretKeysByKeyIdOrNull { get; set; }
    private Dictionary<string, SecretKey> SecretKeysByKeyId => SecretKeysByKeyIdOrNull ??= LoadSecretKeysByKeyId();

    /// <inheritdoc />
    public int Count => SecretKeys.Count;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultSecretKeyCollection"/> class with the specified collection of <see cref="SecretKey"/> items.
    /// The collection will be sorted descending by the <see cref="KeyMetadata.ExpiresWhen"/> property.
    /// </summary>
    /// <param name="items">A collection of <see cref="SecretKey"/> items.</param>
    public DefaultSecretKeyCollection(IEnumerable<SecretKey> items)
    {
        SecretKeys = items.Order(SecretKeyExpiresWhenComparer.Singleton).ToList();
    }

    private Dictionary<string, SecretKey> LoadSecretKeysByKeyId()
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
        SecretKeysByKeyId.TryGetValue(keyId, out secretKey);

    /// <inheritdoc />
    public IEnumerator<SecretKey> GetEnumerator() =>
        SecretKeys.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() =>
        GetEnumerator();
}
