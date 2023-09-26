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

using NCode.Jose.Internal;

namespace NCode.Jose.SecretKeys;

/// <summary>
/// Base class for all cryptographic key material.
/// </summary>
public abstract class SecretKey : BaseDisposable
{
    private string? ToStringOrNull { get; set; }

    /// <summary>
    /// Gets the <c>Key ID (KID)</c> of this <see cref="SecretKey"/>.
    /// </summary>
    public string? KeyId { get; }

    /// <summary>
    /// Gets the intended use for this <see cref="SecretKey"/>. This property is optional and may be <c>null</c> to
    /// indicate that this key is intended for use with any compatible algorithm.
    /// Valid values are defined in RFC 7517 Section 4.2:
    /// https://tools.ietf.org/html/rfc7517#section-4.2
    /// </summary>
    public string? Use { get; }

    /// <summary>
    /// Gets the intended algorithm for use with this <see cref="SecretKey"/>. This property is optional and may be
    /// <c>null</c> to indicate that this key is intended for use with any compatible algorithm.
    /// </summary>
    public string? Algorithm { get; }

    /// <summary>
    /// Gets the size, in bits, of the key material.
    /// </summary>
    public abstract int KeySizeBits { get; }

    /// <summary>
    /// Gets the size, in bytes, of the key material.
    /// </summary>
    public virtual int KeySizeBytes => (KeySizeBits + 7) >> 3;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecretKey"/> class with the specified metadata.
    /// </summary>
    /// <param name="metadata">The metadata for the secret key.</param>
    protected SecretKey(KeyMetadata metadata)
    {
        KeyId = metadata.KeyId;
        Use = metadata.Use;
        Algorithm = metadata.Algorithm;
    }

    /// <summary>
    /// Returns a formatted <see cref="string"/> that represents this <see cref="SecretKey"/> instance.
    /// </summary>
    public override string ToString() => ToStringOrNull ??= $"{GetType().Name} {{ KeyId = {QuoteOrNull(KeyId)}, Use = {QuoteOrNull(Use)}, Algorithm = {QuoteOrNull(Algorithm)}, Size = {KeySizeBits} }}";

    private static string QuoteOrNull(string? value) => value is null ? "(null)" : $"'{value}'";
}
