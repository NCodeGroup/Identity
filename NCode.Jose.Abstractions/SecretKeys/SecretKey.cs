﻿#region Copyright Preamble

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

namespace NCode.Jose.SecretKeys;

/// <summary>
/// Common abstraction for all cryptographic key material.
/// </summary>
public abstract class SecretKey : IDisposable
{
    private string? ToStringOrNull { get; set; }

    /// <summary>
    /// Gets the metadata for this secret key.
    /// </summary>
    public abstract KeyMetadata Metadata { get; }

    /// <summary>
    /// Gets the <c>Key ID (KID)</c> for this secret key.
    /// </summary>
    public virtual string? KeyId => Metadata.KeyId;

    /// <summary>
    /// Gets the size, in bits, of the key material.
    /// For asymmetric keys, this is the size of the modulus.
    /// </summary>
    public abstract int KeySizeBits { get; }

    /// <summary>
    /// Gets the size, in bytes, of the key material.
    /// For asymmetric keys, this is the size of the modulus.
    /// </summary>
    public virtual int KeySizeBytes => (KeySizeBits + 7) >> 3;

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    ~SecretKey()
    {
        Dispose(false);
    }

    /// <summary>
    /// When overridden in a derived class, releases the unmanaged resources used by this instance,
    /// and optionally releases any managed resources.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c>
    /// to release only unmanaged resources.</param>
    protected abstract void Dispose(bool disposing);

    /// <summary>
    /// Returns a formatted <see cref="string"/> that represents this <see cref="SecretKey"/> instance.
    /// </summary>
    public override string ToString() => ToStringOrNull ??= FormatToString();

    /// <summary>
    /// Returns a formatted <see cref="string"/> that represents this <see cref="SecretKey"/> instance.
    /// </summary>
    protected virtual string FormatToString() => $"{GetType().Name} {{ KeyId = {QuoteOrNull(KeyId)}, Size = {KeySizeBits} }}";

    /// <summary>
    /// Returns a quoted <see cref="string"/> or <c>(null)</c> if the value is <c>null</c>.
    /// </summary>
    /// <param name="value">The <see cref="string"/> value to quote.</param>
    /// <returns>The quoted value or <c>null</c>.</returns>
    protected static string QuoteOrNull(string? value) => value is null ? "(null)" : $"'{value}'";
}
