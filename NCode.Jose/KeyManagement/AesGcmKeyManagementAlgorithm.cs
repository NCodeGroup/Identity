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

using System.Diagnostics;
using System.Security.Cryptography;
using NCode.Buffers;
using NCode.Cryptography.Keys;
using NCode.Jose.Exceptions;
using NCode.Jose.Extensions;

namespace NCode.Jose.KeyManagement;

/// <summary>
/// Provides an implementation of <see cref="KeyManagementAlgorithm"/> that uses the <c>AES GCM</c> cryptographic algorithm for key management.
/// </summary>
public class AesGcmKeyManagementAlgorithm : KeyManagementAlgorithm
{
    private const int IvSizeBytes = 96 >> 3;
    private const int TagSizeBytes = 128 >> 3;

    private static IEnumerable<KeySizes> StaticKeyBitSizes { get; } = new KeySizes[]
    {
        new(minSize: 128, maxSize: 256, skipSize: 64)
    };

    /// <inheritdoc />
    public override string Code { get; }

    /// <inheritdoc />
    public override Type KeyType => typeof(SymmetricSecretKey);

    /// <inheritdoc />
    public override IEnumerable<KeySizes> KeyBitSizes => StaticKeyBitSizes;

    private IEnumerable<KeySizes> CekByteSizes { get; }

    private int CekSizeBytes { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AesGcmKeyManagementAlgorithm"/> class.
    /// </summary>
    /// <param name="code">Contains a <see cref="string"/> value that uniquely identifies the cryptographic algorithm.</param>
    /// <param name="cekSizeBits">Contains the legal size, in bits, of the content encryption key (CEK).</param>
    public AesGcmKeyManagementAlgorithm(string code, int cekSizeBits)
    {
        Code = code;
        CekSizeBytes = (cekSizeBits + 7) >> 3;
        CekByteSizes = new[] { new KeySizes(minSize: CekSizeBytes, maxSize: CekSizeBytes, skipSize: 0) };
    }

    /// <inheritdoc />
    public override IEnumerable<KeySizes> GetLegalCekByteSizes(int kekSizeBits) => CekByteSizes;

    /// <inheritdoc />
    public override int GetEncryptedContentKeySizeBytes(int kekSizeBits, int cekSizeBytes) => CekSizeBytes;

    /// <inheritdoc />
    public override bool TryWrapKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        ReadOnlySpan<byte> contentKey,
        Span<byte> encryptedContentKey,
        out int bytesWritten)
    {
        var validatedSecretKey = ValidateSecretKey<SymmetricSecretKey>(secretKey);

        ValidateContentKeySize(secretKey.KeySizeBits, contentKey.Length, nameof(contentKey));

        if (encryptedContentKey.Length < CekSizeBytes)
        {
            bytesWritten = 0;
            return false;
        }

        if (encryptedContentKey.Length > CekSizeBytes)
        {
            encryptedContentKey = encryptedContentKey[..CekSizeBytes];
        }

        using var key = new AesGcm(validatedSecretKey.KeyBytes);

        Span<byte> stack = stackalloc byte[IvSizeBytes + TagSizeBytes];
        var iv = stack[..IvSizeBytes];
        var tag = stack[IvSizeBytes..];

        RandomNumberGenerator.Fill(iv);

        key.Encrypt(iv, contentKey, encryptedContentKey, tag);

        header["iv"] = Base64Url.Encode(iv);
        header["tag"] = Base64Url.Encode(tag);

        bytesWritten = CekSizeBytes;
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

        if (encryptedContentKey.Length != CekSizeBytes)
        {
            throw new ArgumentException(
                "The encrypted content encryption key (CEK) does not have a valid size for this cryptographic algorithm.",
                nameof(encryptedContentKey));
        }

        if (contentKey.Length < CekSizeBytes)
        {
            bytesWritten = 0;
            return false;
        }

        if (contentKey.Length > CekSizeBytes)
        {
            contentKey = contentKey[..CekSizeBytes];
        }

        Span<byte> stack = stackalloc byte[IvSizeBytes + TagSizeBytes];
        var iv = stack[..IvSizeBytes];
        var tag = stack[IvSizeBytes..];

        ValidateHeaderForUnwrap(header, iv, tag);

        using var key = new AesGcm(validatedSecretKey.KeyBytes);

        try
        {
            key.Decrypt(iv, encryptedContentKey, tag, contentKey);
        }
        catch (CryptographicException exception)
        {
            throw new EncryptionJoseException("Failed to decrypt the encrypted content encryption key (CEK).", exception);
        }

        bytesWritten = CekSizeBytes;
        return true;
    }

    private static void ValidateHeaderForUnwrap(
        IReadOnlyDictionary<string, object> header,
        Span<byte> iv,
        Span<byte> tag)
    {
        if (!header.TryGetValue<string>("iv", out var ivString))
        {
            throw new JoseException($"The JWT header is missing the 'iv' field.");
        }

        var ivByteCount = Base64Url.GetByteCountForDecode(ivString.Length);
        if (ivByteCount != IvSizeBytes)
        {
            throw new JoseException($"The 'iv' field in the JWT header has an invalid size.");
        }

        var ivResult = Base64Url.TryDecode(ivString, iv, out var ivBytesWritten);
        Debug.Assert(ivResult && ivBytesWritten == IvSizeBytes);

        if (!header.TryGetValue<string>("tag", out var tagString))
        {
            throw new JoseException($"The JWT header is missing the 'tag' field.");
        }

        var tagByteCount = Base64Url.GetByteCountForDecode(tagString.Length);
        if (tagByteCount != TagSizeBytes)
        {
            throw new JoseException($"The 'tag' field in the JWT header has an invalid size.");
        }

        var tagResult = Base64Url.TryDecode(tagString, tag, out var tagBytesWritten);
        Debug.Assert(tagResult && tagBytesWritten == TagSizeBytes);
    }
}
