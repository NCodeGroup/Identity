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
using System.Text.Json;
using NCode.Jose.Extensions;
using NCode.Jose.SecretKeys;

namespace NCode.Jose.Algorithms.KeyManagement;

/// <summary>
/// Provides an implementation of <see cref="KeyManagementAlgorithm"/> that uses the <c>RSA</c> cryptographic algorithm for key management.
/// </summary>
public class RsaKeyManagementAlgorithm : CommonKeyManagementAlgorithm
{
    private static IEnumerable<KeySizes> StaticKeyBitSizes { get; } = new[]
    {
        new KeySizes(minSize: 2048, maxSize: 16384, skipSize: 64)
    };

    /// <inheritdoc />
    public override string Code { get; }

    /// <inheritdoc />
    public override Type KeyType => typeof(RsaSecretKey);

    /// <inheritdoc />
    public override IEnumerable<KeySizes> KeyBitSizes => StaticKeyBitSizes;

    private RSAEncryptionPadding Padding { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RsaKeyManagementAlgorithm"/> class.
    /// </summary>
    /// <param name="code">Contains a <see cref="string"/> value that uniquely identifies the cryptographic algorithm.</param>
    /// <param name="padding">Contains a <see cref="RSAEncryptionPadding"/> value that describes the type of RSA padding to use.</param>
    public RsaKeyManagementAlgorithm(string code, RSAEncryptionPadding padding)
    {
        Code = code;
        Padding = padding;
    }

    private int GetMaxCekSizeBytes(int kekSizeBits)
    {
        if (Padding.Mode == RSAEncryptionPaddingMode.Oaep)
        {
            // https://crypto.stackexchange.com/a/42100
            // ceil(kLenBits/8) - 2*ceil(hLenBits/8) - 2
            var hLenBits = Padding.OaepHashAlgorithm.GetHashSizeBits();
            var maxSizeBytes = ((kekSizeBits + 7) >> 3) - (((hLenBits + 7) >> 3) << 1) - 2;
            return maxSizeBytes;
        }

        // ReSharper disable once InvertIf
        if (Padding == RSAEncryptionPadding.Pkcs1)
        {
            // ceil(kLenBits/8) - Pkcs1Overhead
            var maxSizeBytes = ((kekSizeBits + 7) >> 3) - 11;
            return maxSizeBytes;
        }

        throw new InvalidOperationException();
    }

    /// <inheritdoc />
    public override IEnumerable<KeySizes> GetLegalCekByteSizes(int kekSizeBits)
    {
        const int octetSize = 1;
        var maxCekSizeBytes = GetMaxCekSizeBytes(kekSizeBits);
        return new[] { new KeySizes(minSize: octetSize, maxCekSizeBytes, skipSize: octetSize) };
    }

    /// <inheritdoc />
    public override int GetEncryptedContentKeySizeBytes(int kekSizeBits, int cekSizeBytes) =>
        (kekSizeBits + 7) >> 3;

    /// <inheritdoc />
    public override bool TryWrapKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        ReadOnlySpan<byte> contentKey,
        Span<byte> encryptedContentKey,
        out int bytesWritten)
    {
        if (encryptedContentKey.Length < secretKey.KeySizeBytes)
        {
            bytesWritten = 0;
            return false;
        }

        var validatedSecretKey = secretKey.Validate<RsaSecretKey>(KeyBitSizes);

        using var key = validatedSecretKey.ExportRSA();

        return key.TryEncrypt(contentKey, encryptedContentKey, Padding, out bytesWritten);
    }

    /// <inheritdoc />
    public override bool TryUnwrapKey(
        SecretKey secretKey,
        JsonElement header,
        ReadOnlySpan<byte> encryptedContentKey,
        Span<byte> contentKey,
        out int bytesWritten)
    {
        var validatedSecretKey = secretKey.Validate<RsaSecretKey>(KeyBitSizes);

        using var key = validatedSecretKey.ExportRSA();

        return key.TryDecrypt(encryptedContentKey, contentKey, Padding, out bytesWritten);
    }
}
