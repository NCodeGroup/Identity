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

using System.Security.Cryptography;
using NCode.Cryptography.Keys;
using NCode.Jose.Exceptions;

namespace NCode.Jose.KeyManagement;

/// <summary>
/// Provides an implementation of <see cref="KeyManagementAlgorithm"/> that uses the <c>key encryption key (KEK)</c> directly for key agreement.
/// </summary>
public class DirectKeyManagementAlgorithm : KeyManagementAlgorithm
{
    private static IEnumerable<KeySizes> StaticKeyBitSizes { get; } = new[]
    {
        new KeySizes(minSize: 8, maxSize: int.MaxValue, skipSize: 8)
    };

    private static IEnumerable<KeySizes> StaticCekByteSizes { get; } = new[]
    {
        new KeySizes(minSize: 1, maxSize: int.MaxValue, skipSize: 1)
    };

    /// <inheritdoc />
    public override string Code => AlgorithmCodes.KeyManagement.Direct;

    /// <inheritdoc />
    public override Type KeyType => typeof(SymmetricSecretKey);

    /// <inheritdoc />
    public override IEnumerable<KeySizes> KeyBitSizes => StaticKeyBitSizes;

    /// <inheritdoc />
    public override IEnumerable<KeySizes> GetLegalCekByteSizes(int kekSizeBits) => StaticCekByteSizes;

    /// <inheritdoc />
    public override int GetEncryptedContentKeySizeBytes(int kekSizeBits, int cekSizeBytes) => 0;

    /// <inheritdoc />
    public override void NewKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        Span<byte> contentKey)
    {
        var validatedSecretKey = ValidateSecretKey<SymmetricSecretKey>(secretKey);

        if (contentKey.Length != secretKey.KeySizeBytes)
        {
            throw new JoseException("The size of the destination buffer for CEK must identical to the KEK size.");
        }

        validatedSecretKey.KeyBytes.CopyTo(contentKey);
    }

    /// <inheritdoc />
    public override bool TryWrapKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        ReadOnlySpan<byte> contentKey,
        Span<byte> encryptedContentKey,
        out int bytesWritten)
    {
        throw new JoseException("The direct key management algorithm does not support using an existing CEK.");
    }

    /// <inheritdoc />
    public override bool TryWrapNewKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        Span<byte> contentKey,
        Span<byte> encryptedContentKey,
        out int bytesWritten)
    {
        NewKey(secretKey, header, contentKey);
        bytesWritten = 0;
        return true;
    }

    /// <inheritdoc />
    public override bool TryUnwrapKey(
        SecretKey secretKey,
        IReadOnlyDictionary<string, object> header,
        ReadOnlySpan<byte> encryptedContentKey,
        Span<byte> contentKey,
        out int bytesWritten)
    {
        var validatedSecretKey = ValidateSecretKey<SymmetricSecretKey>(secretKey);

        if (encryptedContentKey.Length != 0)
        {
            throw new JoseException("The encrypted content encryption key (CEK) does not have a valid size for this cryptographic algorithm.");
        }

        if (contentKey.Length < secretKey.KeySizeBytes)
        {
            bytesWritten = 0;
            return false;
        }

        var result = validatedSecretKey.KeyBytes.TryCopyTo(contentKey);
        bytesWritten = result ? secretKey.KeySizeBytes : 0;
        return result;
    }
}
