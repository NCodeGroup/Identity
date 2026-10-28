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

using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.Algorithms.KeyManagement;

/// <summary>
/// Provides an implementation of <see cref="KeyManagementAlgorithm"/> that uses <c>RSAES-OAEP</c> for key management:
/// <c>RSA-OAEP</c>, <c>RSA-OAEP-256</c>, <c>RSA-OAEP-384</c> and <c>RSA-OAEP-512</c>.
/// </summary>
/// <param name="code">Contains a <see cref="string"/> value that uniquely identifies the cryptographic algorithm.</param>
/// <param name="hashAlgorithmName">The hash function that <c>OAEP</c> and its <c>MGF1</c> mask generation function use.</param>
internal class RsaOaepKeyManagementAlgorithm(string code, HashAlgorithmName hashAlgorithmName)
    : CommonKeyManagementAlgorithm
{
    private static IEnumerable<KeySizes> StaticKeyBitSizes { get; } =
    [new(minSize: 2048, maxSize: 16384, skipSize: 64)];

    /// <inheritdoc />
    public override string Code { get; } = code;

    /// <inheritdoc />
    public override Type KeyType => typeof(RsaSecretKey);

    /// <inheritdoc />
    public override IEnumerable<KeySizes> KeyBitSizes => StaticKeyBitSizes;

    private RSAEncryptionPadding Padding { get; } =
        RSAEncryptionPadding.CreateOaep(hashAlgorithmName);

    private int GetMaxCekSizeBytes(int kekSizeBits)
    {
        // https://crypto.stackexchange.com/a/42100
        // ceil(kLenBits/8) - 2*ceil(hLenBits/8) - 2
        var hLenBits = Padding.OaepHashAlgorithm.GetHashSizeBits();
        var maxSizeBytes = ((kekSizeBits + 7) >> 3) - (((hLenBits + 7) >> 3) << 1) - 2;
        return maxSizeBytes;
    }

    /// <inheritdoc />
    public override IEnumerable<KeySizes> GetLegalCekByteSizes(int kekSizeBits)
    {
        const int octetSize = 1;
        var maxCekSizeBytes = GetMaxCekSizeBytes(kekSizeBits);
        return [new KeySizes(minSize: octetSize, maxCekSizeBytes, skipSize: octetSize)];
    }

    /// <inheritdoc />
    public override int GetEncryptedContentKeySizeBytes(int kekSizeBits, int cekSizeBytes) =>
        (kekSizeBits + 7) >> 3;

    /// <inheritdoc />
    public override void WrapKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        ReadOnlySpan<byte> contentKey,
        IBufferWriter<byte> encryptedContentKeyWriter
    )
    {
        var validatedSecretKey = secretKey.Validate<RsaSecretKey>(KeyBitSizes);

        using var key = validatedSecretKey.ExportRSA();

        var encryptedContentKey = encryptedContentKeyWriter.GetSpan(secretKey.KeySizeBytes);

        var bytesWritten = key.Encrypt(contentKey, encryptedContentKey, Padding);

        encryptedContentKeyWriter.Advance(bytesWritten);
    }

    /// <inheritdoc />
    public override bool TryUnwrapKey(
        SecretKey secretKey,
        JsonElement header,
        ReadOnlySpan<byte> encryptedContentKey,
        Span<byte> contentKey,
        out int bytesWritten
    )
    {
        var validatedSecretKey = secretKey.Validate<RsaSecretKey>(KeyBitSizes);

        using var key = validatedSecretKey.ExportRSA();

        return key.TryDecrypt(encryptedContentKey, contentKey, Padding, out bytesWritten);
    }
}
