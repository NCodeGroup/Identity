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

using NCode.Cryptography.Keys.Internal;

namespace NCode.Cryptography.Keys;

/// <summary>
/// Base class for all cryptographic key material.
/// </summary>
public abstract class SecretKey : BaseDisposable
{
    /// <summary>
    /// Gets the <c>Key ID (KID)</c> of this <see cref="SecretKey"/>.
    /// </summary>
    public string KeyId { get; }

    /// <summary>
    /// Gets a read-only set of tags associated with this <see cref="SecretKey"/>.
    /// </summary>
    public IReadOnlySet<string> Tags { get; }

    /// <summary>
    /// Gets the size, in bits, of the key material.
    /// </summary>
    public abstract int KeySizeBits { get; }

    /// <summary>
    /// Gets the size, in bytes, of the key material.
    /// </summary>
    public virtual int KeySizeBytes => (KeySizeBits + 7) >> 3;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecretKey"/> class with the specified <c>Key ID (KID)</c>.
    /// </summary>
    /// <param name="keyId">The <c>Key ID (KID)</c> for the secret key.</param>
    /// <param name="tags">The collection of tags associated with the secret key.</param>
    protected SecretKey(string keyId, IEnumerable<string> tags)
    {
        KeyId = keyId;
        Tags = tags.ToHashSet(StringComparer.Ordinal);
    }
}
