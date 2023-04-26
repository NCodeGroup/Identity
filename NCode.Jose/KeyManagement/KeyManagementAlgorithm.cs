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

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using NCode.Cryptography.Keys;

namespace NCode.Jose.KeyManagement;

/// <summary>
/// Provides methods for all cryptographic key management algorithms.
/// </summary>
public interface IKeyManagementAlgorithm : IAlgorithm
{
    IEnumerable<KeySizes> GetLegalCekByteSizes(int kekSizeBits);

    int GetEncryptedContentKeySizeBytes(
        int kekSizeBits,
        int cekSizeBytes);

    void NewKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        Span<byte> contentKey);

    bool TryWrapKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        ReadOnlySpan<byte> contentKey,
        Span<byte> encryptedContentKey,
        out int bytesWritten);

    bool TryUnwrapKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        ReadOnlySpan<byte> encryptedContentKey,
        Span<byte> contentKey,
        out int bytesWritten);
}

/// <summary>
/// Base implementation for all cryptographic key management algorithms.
/// </summary>
public abstract class KeyManagementAlgorithm : Algorithm, IKeyManagementAlgorithm
{
    /// <inheritdoc />
    public override AlgorithmType Type => AlgorithmType.KeyManagement;

    /// <inheritdoc />
    public abstract IEnumerable<KeySizes> GetLegalCekByteSizes(int kekSizeBits);

    /// <inheritdoc />
    public abstract int GetEncryptedContentKeySizeBytes(
        int kekSizeBits,
        int cekSizeBytes);

    /// <inheritdoc />
    public virtual void NewKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        Span<byte> contentKey)
    {
        RandomNumberGenerator.Fill(contentKey);
    }

    /// <inheritdoc />
    public abstract bool TryWrapKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        ReadOnlySpan<byte> contentKey,
        Span<byte> encryptedContentKey,
        out int bytesWritten);

    /// <inheritdoc />
    public abstract bool TryUnwrapKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        ReadOnlySpan<byte> encryptedContentKey,
        Span<byte> contentKey,
        out int bytesWritten);

    /// <summary>
    /// Attempts to retrieve a typed key-value pair from a dictionary.
    /// </summary>
    /// <param name="header">The dictionary containing the key-value pairs.</param>
    /// <param name="key">The key of the value to retrieve.</param>
    /// <param name="value">When this method returns, the value associated with the specified key, if the key is found;
    /// otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
    /// <typeparam name="T">The type of value to retrieve.</typeparam>
    /// <returns><c>true</c> if a value with the specified key was found; otherwise, <c>false</c>.</returns>
    protected static bool TryGetHeader<T>(IDictionary<string, object> header, string key, [MaybeNullWhen(false)] out T value)
    {
        if (header.TryGetValue(key, out var obj) && obj is T typedValue)
        {
            value = typedValue;
            return true;
        }

        value = default;
        return false;
    }
}
