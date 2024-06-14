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
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NCode.CryptoMemory;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Algorithms.Compression;
using NCode.Identity.Jose.Encoders;
using NCode.Identity.Jose.Exceptions;
using NCode.Identity.Jose.Extensions;
using NCode.Identity.Secrets;
using Nerdbank.Streams;

namespace NCode.Identity.Jose;

partial class JoseSerializer
{
    private KeyManagementAlgorithm GetKeyManagementAlgorithm(string code) =>
        !AlgorithmCollection.TryGetKeyManagementAlgorithm(code, out var algorithm) ?
            throw new JoseInvalidAlgorithmException($"The `{code}` algorithm is not supported for key management.") :
            AssertEnabled(algorithm);

    private AuthenticatedEncryptionAlgorithm GetAuthenticatedEncryptionAlgorithm(string code) =>
        !AlgorithmCollection.TryGetAuthenticatedEncryptionAlgorithm(code, out var algorithm) ?
            throw new JoseInvalidAlgorithmException($"The `{code}` algorithm is not supported for AEAD encryption.") :
            AssertEnabled(algorithm);

    private CompressionAlgorithm GetCompressionAlgorithm(string code) =>
        !AlgorithmCollection.TryGetCompressionAlgorithm(code, out var algorithm) ?
            throw new JoseInvalidAlgorithmException($"The `{code}` algorithm is not supported for compression.") :
            AssertEnabled(algorithm);

    /// <inheritdoc />
    public JoseEncoder CreateEncoder(JoseEncryptionOptions encryptionOptions) =>
        new JoseEncryptionEncoder(this, encryptionOptions);

    /// <inheritdoc />
    public string Encode<T>(
        T payload,
        JoseEncryptionOptions encryptionOptions,
        JsonSerializerOptions? jsonOptions = null,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        using var tokenBuffer = new Sequence<char>();
        using var _ = SerializeToUtf8(
            payload,
            jsonOptions,
            out var payloadBytes);
        Encode(
            tokenBuffer,
            payloadBytes,
            encryptionOptions,
            extraHeaders);
        return tokenBuffer.AsReadOnlySequence.ToString();
    }

    /// <inheritdoc />
    public void Encode<T>(
        IBufferWriter<char> tokenWriter,
        T payload,
        JoseEncryptionOptions encryptionOptions,
        JsonSerializerOptions? jsonOptions = null,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        using var _ = SerializeToUtf8(
            payload,
            jsonOptions,
            out var payloadBytes);
        Encode(
            tokenWriter,
            payloadBytes,
            encryptionOptions,
            extraHeaders);
    }

    /// <inheritdoc />
    public void Encode(
        IBufferWriter<char> tokenWriter,
        string payload,
        JoseEncryptionOptions encryptionOptions,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        Encode(
            tokenWriter,
            payload.AsSpan(),
            encryptionOptions,
            extraHeaders);
    }

    /// <inheritdoc />
    public string Encode(
        string payload,
        JoseEncryptionOptions encryptionOptions,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        return Encode(
            payload.AsSpan(),
            encryptionOptions,
            extraHeaders);
    }

    /// <inheritdoc />
    public void Encode(
        IBufferWriter<char> tokenWriter,
        ReadOnlySpan<char> payload,
        JoseEncryptionOptions encryptionOptions,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        var byteCount = SecureEncoding.UTF8.GetByteCount(payload);
        using var payloadLease = CryptoPool.Rent(byteCount, isSensitive: false, out Span<byte> payloadBytes);
        var bytesWritten = SecureEncoding.UTF8.GetBytes(payload, payloadBytes);
        Debug.Assert(bytesWritten == byteCount);
        Encode(
            tokenWriter,
            payloadBytes,
            encryptionOptions,
            extraHeaders);
    }

    /// <inheritdoc />
    public string Encode(
        ReadOnlySpan<char> payload,
        JoseEncryptionOptions encryptionOptions,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        using var tokenBuffer = new Sequence<char>();
        Encode(
            tokenBuffer,
            payload,
            encryptionOptions,
            extraHeaders);
        return tokenBuffer.AsReadOnlySequence.ToString();
    }

    /// <inheritdoc />
    public string Encode(
        ReadOnlySpan<byte> payload,
        JoseEncryptionOptions encryptionOptions,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        using var tokenBuffer = new Sequence<char>();
        Encode(
            tokenBuffer,
            payload,
            encryptionOptions,
            extraHeaders);
        return tokenBuffer.AsReadOnlySequence.ToString();
    }

    /// <inheritdoc />
    public void Encode(
        IBufferWriter<char> tokenWriter,
        ReadOnlySpan<byte> payload,
        JoseEncryptionOptions encryptionOptions,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        var header = extraHeaders != null ?
            new Dictionary<string, object>(extraHeaders) :
            new Dictionary<string, object>();

        var encryptionCredentials = encryptionOptions.EncryptionCredentials;
        var (secretKey, keyManagementAlgorithm, authenticatedEncryptionAlgorithm, compressionAlgorithm) = encryptionCredentials;

        header[JoseClaimNames.Header.Alg] = keyManagementAlgorithm.Code;
        header[JoseClaimNames.Header.Enc] = authenticatedEncryptionAlgorithm.Code;

        var tokenType = encryptionOptions.TokenType;
        if (!string.IsNullOrEmpty(tokenType))
            header[JoseClaimNames.Header.Typ] = tokenType;

        var keyId = secretKey.KeyId;
        if (!string.IsNullOrEmpty(keyId) && encryptionOptions.AddKeyIdHeader)
            header[JoseClaimNames.Header.Kid] = keyId;

        var cekSizeBytes = authenticatedEncryptionAlgorithm.ContentKeySizeBytes;
        using var cekLease = CryptoPool.Rent(cekSizeBytes, isSensitive: true, out Span<byte> cek);

        var encryptedCekSizeBytes = keyManagementAlgorithm.GetEncryptedContentKeySizeBytes(secretKey.KeySizeBits, cekSizeBytes);
        using var encryptedCekLease = CryptoPool.Rent(encryptedCekSizeBytes, isSensitive: false, out Span<byte> encryptedCek);

        var wrapResult = keyManagementAlgorithm.TryWrapNewKey(
            secretKey,
            header,
            cek,
            encryptedCek,
            out var encryptedCekBytesWritten);
        Debug.Assert(wrapResult && encryptedCekBytesWritten == encryptedCekSizeBytes);

        var nonceSizeBytes = authenticatedEncryptionAlgorithm.NonceSizeBytes;
        var nonce = nonceSizeBytes <= JoseConstants.MaxStackAlloc ?
            stackalloc byte[nonceSizeBytes] :
            GC.AllocateUninitializedArray<byte>(nonceSizeBytes, pinned: false);
        RandomNumberGenerator.Fill(nonce);

        var tagSizeBytes = authenticatedEncryptionAlgorithm.AuthenticationTagSizeBytes;
        var tag = tagSizeBytes <= JoseConstants.MaxStackAlloc ?
            stackalloc byte[tagSizeBytes] :
            GC.AllocateUninitializedArray<byte>(tagSizeBytes, pinned: false);

        // compression must be done before associated data because compression modifies the header
        // and the header is used to derive the associated data
        using var plainTextLease = compressionAlgorithm.Compress(header, payload, out var plainText);
        using var headerLease = EncodeJose(b64: true, header, out var encodedHeader);
        using var aadLease = Encode(Encoding.ASCII, encodedHeader, out var aad);

        var cipherTextSizeBytes = authenticatedEncryptionAlgorithm.GetCipherTextSizeBytes(plainText.Length);
        using var cipherTextLease = CryptoPool.Rent(cipherTextSizeBytes, isSensitive: false, out Span<byte> cipherText);
        authenticatedEncryptionAlgorithm.Encrypt(cek, nonce, plainText, aad, cipherText, tag);

        // BASE64URL(UTF8(JWE Protected Header)) || '.' ||
        WriteCompactSegment(encodedHeader, tokenWriter);

        // BASE64URL(JWE Encrypted Key) || '.' ||
        WriteCompactSegment(encryptedCek, tokenWriter);

        // BASE64URL(JWE Initialization Vector) || '.' ||
        WriteCompactSegment(nonce, tokenWriter);

        // BASE64URL(JWE Ciphertext) || '.' ||
        WriteCompactSegment(cipherText, tokenWriter);

        // BASE64URL(JWE Authentication Tag)
        WriteCompactSegment(tag, tokenWriter, addDot: false);
    }

    private string DecodeJwe(CompactJwt compactJwt, SecretKey secretKey)
    {
        using var byteBuffer = new Sequence<byte>(ArrayPool<byte>.Shared);
        DecryptJwe(compactJwt, secretKey, byteBuffer);
        return DecodeUtf8(byteBuffer);
    }

    private T? DeserializeJwe<T>(CompactJwt compactJwt, SecretKey secretKey)
    {
        using var byteBuffer = new Sequence<byte>(ArrayPool<byte>.Shared);
        DecryptJwe(compactJwt, secretKey, byteBuffer);
        return Deserialize<T>(byteBuffer);
    }

    private void DecryptJwe(CompactJwt compactJwt, SecretKey secretKey, IBufferWriter<byte> payloadBytes)
    {
        /*
              BASE64URL(UTF8(JWE Protected Header)) || '.' ||
              BASE64URL(JWE Encrypted Key) || '.' ||
              BASE64URL(JWE Initialization Vector) || '.' ||
              BASE64URL(JWE Ciphertext) || '.' ||
              BASE64URL(JWE Authentication Tag)
        */
        Debug.Assert(compactJwt.ProtectionType == JoseProtectionTypes.Jwe);

        // JWE Protected Header
        var jweProtectedHeader = compactJwt.Segments.First;
        var header = compactJwt.DeserializedHeader;

        // JWE Encrypted Key
        var jweEncryptedKey = jweProtectedHeader.Next!;
        var encodedEncryptedKey = jweEncryptedKey.Memory.Span;
        using var encryptedKeyLease = DecodeBase64Url(
            encodedEncryptedKey,
            isSensitive: true,
            out var encryptedKeyBytes);

        // JWE Initialization Vector
        var jweInitializationVector = jweEncryptedKey.Next!;
        var encodedInitializationVector = jweInitializationVector.Memory.Span;
        using var initializationVectorLease = DecodeBase64Url(
            encodedInitializationVector,
            isSensitive: false,
            out var initializationVectorBytes);

        // JWE Ciphertext
        var jweCiphertext = jweInitializationVector.Next!;
        var encodedCiphertext = jweCiphertext.Memory.Span;
        using var cipherTextLease = DecodeBase64Url(
            encodedCiphertext,
            isSensitive: false,
            out var cipherTextBytes);

        // JWE Authentication Tag
        var jweAuthenticationTag = jweCiphertext.Next!;
        var encodedAuthenticationTag = jweAuthenticationTag.Memory.Span;
        using var authenticationTagLease = DecodeBase64Url(
            encodedAuthenticationTag,
            isSensitive: false,
            out var authenticationTagBytes);

        if (!header.TryGetPropertyValue<string>(JoseClaimNames.Header.Alg, out var keyManagementAlgorithmCode))
            throw new JoseException("The JWE header is missing the 'alg' field.");

        if (!header.TryGetPropertyValue<string>(JoseClaimNames.Header.Enc, out var authenticatedEncryptionAlgorithmCode))
            throw new JoseException("The JWE header is missing the 'enc' field.");

        var keyManagementAlgorithm = GetKeyManagementAlgorithm(keyManagementAlgorithmCode);
        var authenticatedEncryptionAlgorithm = GetAuthenticatedEncryptionAlgorithm(authenticatedEncryptionAlgorithmCode);

        var cekSizeBytes = authenticatedEncryptionAlgorithm.ContentKeySizeBytes;
        using var contentKeyLease = CryptoPool.Rent(cekSizeBytes, isSensitive: true, out Span<byte> contentKey);

        var unwrapResult = keyManagementAlgorithm.TryUnwrapKey(
            secretKey,
            header,
            encryptedKeyBytes,
            contentKey,
            out var unwrapBytesWritten);

        if (!unwrapResult || unwrapBytesWritten == 0)
            throw new JoseEncryptionException("Failed to decrypt the encrypted content encryption key (CEK).");

        if (unwrapBytesWritten < cekSizeBytes)
            contentKey = contentKey[..unwrapBytesWritten];

        /*
           14.  Compute the Encoded Protected Header value BASE64URL(UTF8(JWE
                Protected Header)).  If the JWE Protected Header is not present
                (which can only happen when using the JWE JSON Serialization and
                no "protected" member is present), let this value be the empty
                string.

           15.  Let the Additional Authenticated Data encryption parameter be
                ASCII(Encoded Protected Header).  However if a JWE AAD value is
                present (which can only be the case when using the JWE JSON
                Serialization), instead let the Additional Authenticated Data
                encryption parameter be ASCII(Encoded Protected Header || '.' ||
                BASE64URL(JWE AAD)).
        */

        using var associatedDataLease = Encode(Encoding.ASCII, compactJwt.EncodedHeader, out var associatedDataBytes);

        var plainTextSizeBytes = authenticatedEncryptionAlgorithm.GetMaxPlainTextSizeBytes(cipherTextBytes.Length);
        using var plainTextLease = CryptoPool.Rent(plainTextSizeBytes, isSensitive: false, out Span<byte> plainTextBytes);

        /*
           16.  Decrypt the JWE Ciphertext using the CEK, the JWE Initialization
                Vector, the Additional Authenticated Data value, and the JWE
                Authentication Tag (which is the Authentication Tag input to the
                calculation) using the specified content encryption algorithm,
                returning the decrypted plaintext and validating the JWE
                Authentication Tag in the manner specified for the algorithm,
                rejecting the input without emitting any decrypted output if the
                JWE Authentication Tag is incorrect.
        */

        var decryptResult = authenticatedEncryptionAlgorithm.TryDecrypt(
            contentKey,
            initializationVectorBytes,
            cipherTextBytes,
            associatedDataBytes,
            authenticationTagBytes,
            plainTextBytes,
            out var decryptBytesWritten);

        if (!decryptResult || decryptBytesWritten == 0)
            throw new JoseEncryptionException("Failed to decrypt the JWE Ciphertext.");

        if (decryptBytesWritten < plainTextSizeBytes)
            plainTextBytes = plainTextBytes[..decryptBytesWritten];

        /*
           17.  If a "zip" parameter was included, uncompress the decrypted
                plaintext using the specified compression algorithm.
        */

        if (header.TryGetPropertyValue<string>(JoseClaimNames.Header.Zip, out var compressionAlgorithmCode))
        {
            var compressionAlgorithm = GetCompressionAlgorithm(compressionAlgorithmCode);
            compressionAlgorithm.Decompress(plainTextBytes, payloadBytes);
        }
        else
        {
            payloadBytes.Write(plainTextBytes);
        }
    }
}
