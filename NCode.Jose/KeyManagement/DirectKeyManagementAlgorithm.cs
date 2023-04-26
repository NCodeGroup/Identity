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

namespace NCode.Jose.KeyManagement;

public class DirectKeyManagementAlgorithm : KeyManagementAlgorithm
{
    private static IEnumerable<KeySizes> StaticKekBitSizes { get; } = new[]
    {
        new KeySizes(minSize: 8, maxSize: int.MaxValue, skipSize: 8)
    };

    /// <inheritdoc />
    public override string Code => "dir";

    /// <inheritdoc />
    public override Type SecretKeyType => typeof(SymmetricSecretKey);

    /// <inheritdoc />
    public override IEnumerable<KeySizes> KekBitSizes => StaticKekBitSizes;

    /// <inheritdoc />
    public override int GetEncryptedContentKeySizeBytes(
        int kekSizeBits,
        int cekSizeBytes) => 0;

    /// <inheritdoc />
    public override void NewKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        Span<byte> contentKey)
    {
        var validatedSecretKey = ValidateSecretKey<SymmetricSecretKey>(secretKey);

        if (contentKey.Length != secretKey.KeySizeBytes)
        {
            throw new ArgumentException("Destination buffer too small.", nameof(contentKey));
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
        throw new NotSupportedException("The 'none' key management algorithm does not support using an existing CEK.");
    }

    /// <inheritdoc />
    public override bool TryUnwrapKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        ReadOnlySpan<byte> encryptedContentKey,
        Span<byte> contentKey,
        out int bytesWritten)
    {
        if (encryptedContentKey.Length != 0)
        {
            throw new ArgumentException(
                "The encrypted content encryption key (CEK) does not have a valid size for this cryptographic algorithm.",
                nameof(encryptedContentKey));
        }

        if (contentKey.Length < secretKey.KeySizeBytes)
        {
            bytesWritten = 0;
            return false;
        }

        var validatedSecretKey = ValidateSecretKey<SymmetricSecretKey>(secretKey);

        var result = validatedSecretKey.KeyBytes.TryCopyTo(contentKey);
        bytesWritten = result ? secretKey.KeySizeBytes : 0;
        return result;
    }
}
