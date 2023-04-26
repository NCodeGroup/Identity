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
    public AlgorithmType Type => AlgorithmType.KeyManagement;

    /// <inheritdoc />
    public abstract string Code { get; }

    /// <inheritdoc />
    public abstract Type SecretKeyType { get; }

    /// <inheritdoc />
    public abstract IEnumerable<KeySizes> KekBitSizes { get; }

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

    protected bool TryGetHeader<T>(IDictionary<string, object> header, string key, [MaybeNullWhen(false)] out T value)
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
