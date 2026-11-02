// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Secrets.Collections;

/// <summary>
/// Provides an <see cref="ISecretKeyCollection"/> implementation that is empty.
/// </summary>
internal sealed class EmptySecretKeyCollection : ISecretKeyCollection
{
    /// <summary>
    /// Gets a singleton instance of <see cref="EmptySecretKeyCollection"/>.
    /// </summary>
    public static EmptySecretKeyCollection Singleton { get; } = new();

    /// <inheritdoc />
    public int Count => 0;

    /// <inheritdoc />
    public bool TryGetByKeyId(string keyId, [MaybeNullWhen(false)] out SecretKey secretKey)
    {
        secretKey = null;
        return false;
    }

    /// <inheritdoc />
    public IEnumerator<SecretKey> GetEnumerator() => Enumerable.Empty<SecretKey>().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
