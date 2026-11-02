// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Secrets.Collections;

/// <summary>
/// Provides a default implementation for the <see cref="ISecretKeyCollection"/> interface.
/// The collection is sorted descending by the <see cref="KeyMetadata.ExpiresWhen"/> property.
/// </summary>
/// <param name="items">A collection of <see cref="SecretKey"/> items.</param>
internal class DefaultSecretKeyCollection(IEnumerable<SecretKey> items) : ISecretKeyCollection
{
    private List<SecretKey> SecretKeys { get; } =
        items.Order(SecretKeyExpiresWhenComparer.Singleton).ToList();
    private Dictionary<string, SecretKey>? SecretKeysByKeyIdOrNull { get; set; }
    private Dictionary<string, SecretKey> SecretKeysByKeyId =>
        SecretKeysByKeyIdOrNull ??= LoadSecretKeysByKeyId();

    /// <inheritdoc />
    public int Count => SecretKeys.Count;

    private Dictionary<string, SecretKey> LoadSecretKeysByKeyId()
    {
        var dictionary = new Dictionary<string, SecretKey>(StringComparer.Ordinal);

        foreach (var secretKey in SecretKeys)
        {
            var keyId = secretKey.KeyId;
            if (string.IsNullOrEmpty(keyId))
                continue;
            dictionary.TryAdd(keyId, secretKey);
        }

        return dictionary;
    }

    /// <inheritdoc />
    public bool TryGetByKeyId(string keyId, [MaybeNullWhen(false)] out SecretKey secretKey) =>
        SecretKeysByKeyId.TryGetValue(keyId, out secretKey);

    /// <inheritdoc />
    public IEnumerator<SecretKey> GetEnumerator() => SecretKeys.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
