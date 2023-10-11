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

namespace NCode.Jose.SecretKeys;

/// <summary>
/// Provides an <see cref="ISecretKeyCollection"/> implementation that is empty.
/// </summary>
public sealed class EmptySecretKeyCollection : ISecretKeyCollection
{
    /// <summary>
    /// Gets a singleton instance of <see cref="EmptySecretKeyCollection"/>.
    /// </summary>
    public static EmptySecretKeyCollection Singleton { get; } = new();

    /// <inheritdoc />
    public int Count => 0;

    private EmptySecretKeyCollection()
    {
        // nothing
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // nothing
    }

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
