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
using System.Text.Json;
using NCode.Encoders;
using NCode.Jose.Exceptions;
using NCode.Jose.Json;
using NCode.Jose.SecretKeys;

namespace NCode.Jose.Algorithms.KeyManagement;

/// <summary>
/// Provides an implementation of <see cref="KeyManagementAlgorithm"/> that uses the <c>AES GCM</c> cryptographic algorithm for key management.
/// </summary>
public class AesGcmKeyManagementAlgorithm : KeyManagementAlgorithm
{
    private const int IvSizeBytes = 96 >> 3;
    private const int TagSizeBytes = 128 >> 3;

    private static IEnumerable<KeySizes> StaticCekByteSizes { get; } = new[]
    {
        new KeySizes(minSize: 1, maxSize: int.MaxValue, skipSize: 1)
    };

    /// <inheritdoc />
    public override string Code { get; }

    /// <inheritdoc />
    public override Type KeyType => typeof(SymmetricSecretKey);

    /// <inheritdoc />
    public override IEnumerable<KeySizes> KeyBitSizes { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AesGcmKeyManagementAlgorithm"/> class.
    /// </summary>
    /// <param name="code">Contains a <see cref="string"/> value that uniquely identifies the cryptographic algorithm.</param>
    /// <param name="kekSizeBits">Contains the legal size, in bits, of the key encryption key (KEK).</param>
    public AesGcmKeyManagementAlgorithm(string code, int kekSizeBits)
    {
        Code = code;

        KeyBitSizes = new[] { new KeySizes(minSize: kekSizeBits, maxSize: kekSizeBits, skipSize: 0) };
    }

    /// <inheritdoc />
    public override IEnumerable<KeySizes> GetLegalCekByteSizes(int kekSizeBits) => StaticCekByteSizes;

    /// <inheritdoc />
    public override int GetEncryptedContentKeySizeBytes(int kekSizeBits, int cekSizeBytes) => cekSizeBytes;

    /// <inheritdoc />
    public override bool TryWrapKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        ReadOnlySpan<byte> contentKey,
        Span<byte> encryptedContentKey,
        out int bytesWritten)
    {
        var validatedSecretKey = ValidateSecretKey<SymmetricSecretKey>(secretKey);

        // Output size = Input size
        if (encryptedContentKey.Length < contentKey.Length)
        {
            bytesWritten = 0;
            return false;
        }

        using var key = new AesGcm(validatedSecretKey.KeyBytes);

        Span<byte> stack = stackalloc byte[IvSizeBytes + TagSizeBytes];
        var iv = stack[..IvSizeBytes];
        var tag = stack[IvSizeBytes..];

        RandomNumberGenerator.Fill(iv);

        key.Encrypt(iv, contentKey, encryptedContentKey, tag);

        header[JoseClaimNames.Header.Iv] = Base64Url.Encode(iv);
        header[JoseClaimNames.Header.Tag] = Base64Url.Encode(tag);

        bytesWritten = contentKey.Length;
        return true;
    }

    /// <inheritdoc />
    public override bool TryUnwrapKey(
        SecretKey secretKey,
        JsonElement header,
        ReadOnlySpan<byte> encryptedContentKey,
        Span<byte> contentKey,
        out int bytesWritten)
    {
        var validatedSecretKey = ValidateSecretKey<SymmetricSecretKey>(secretKey);

        // Output size = Input size
        if (contentKey.Length < encryptedContentKey.Length)
        {
            bytesWritten = 0;
            return false;
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

        bytesWritten = encryptedContentKey.Length;
        return true;
    }

    internal static void ValidateHeaderForUnwrap(
        JsonElement header,
        Span<byte> iv,
        Span<byte> tag)
    {
        if (!header.TryGetPropertyValue<string>(JoseClaimNames.Header.Iv, out var ivString))
        {
            throw new JoseException("The JWT header is missing the 'iv' field.");
        }

        var ivByteCount = Base64Url.GetByteCountForDecode(ivString.Length);
        if (ivByteCount != IvSizeBytes)
        {
            throw new JoseException("The 'iv' field in the JWT header has an invalid size.");
        }

        try
        {
            var ivResult = Base64Url.TryDecode(ivString, iv, out var ivBytesWritten);
            Debug.Assert(ivResult && ivBytesWritten == IvSizeBytes);
        }
        catch (Exception exception)
        {
            throw new JoseException("Failed to deserialize the 'iv' field from the JWT header.", exception);
        }

        if (!header.TryGetPropertyValue<string>(JoseClaimNames.Header.Tag, out var tagString))
        {
            throw new JoseException("The JWT header is missing the 'tag' field.");
        }

        var tagByteCount = Base64Url.GetByteCountForDecode(tagString.Length);
        if (tagByteCount != TagSizeBytes)
        {
            throw new JoseException("The 'tag' field in the JWT header has an invalid size.");
        }

        try
        {
            var tagResult = Base64Url.TryDecode(tagString, tag, out var tagBytesWritten);
            Debug.Assert(tagResult && tagBytesWritten == TagSizeBytes);
        }
        catch (Exception exception)
        {
            throw new JoseException("Failed to deserialize the 'tag' field from the JWT header.", exception);
        }
    }
}
