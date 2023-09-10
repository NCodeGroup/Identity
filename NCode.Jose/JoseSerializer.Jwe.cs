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
using NCode.CryptoMemory;
using NCode.Jose.Algorithms.AuthenticatedEncryption;
using NCode.Jose.Algorithms.Compression;
using NCode.Jose.Algorithms.KeyManagement;
using NCode.Jose.Exceptions;
using NCode.Jose.Json;
using NCode.Jose.SecretKeys;
using Nerdbank.Streams;

namespace NCode.Jose;

partial interface IJoseSerializer
{
    /// <summary>
    /// Encrypts a JWE token given the specified payload.
    /// </summary>
    /// <param name="payload">The payload to encrypt.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for encryption.</param>
    /// <param name="keyManagementAlgorithmCode">The <c>Code</c> of the <see cref="IKeyManagementAlgorithm"/> to use for key management.</param>
    /// <param name="encryptionAlgorithmCode">The <c>Code</c> of the <see cref="IAuthenticatedEncryptionAlgorithm"/> to use for encryption.</param>
    /// <param name="compressionAlgorithmCode">The <c>Code</c> of the <see cref="ICompressionAlgorithm"/> to use for compression.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    /// <typeparam name="T">The type of the payload to encode.</typeparam>
    /// <returns>The encrypted JWE token.</returns>
    string EncodeJwe<T>(
        T payload,
        SecretKey secretKey,
        string keyManagementAlgorithmCode,
        string encryptionAlgorithmCode,
        string? compressionAlgorithmCode,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null);

    /// <summary>
    /// Encrypts a JWE token given the specified payload.
    /// </summary>
    /// <param name="tokenWriter">The destination for the encrypted JWE token.</param>
    /// <param name="payload">The payload to encrypt.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for encryption.</param>
    /// <param name="keyManagementAlgorithmCode">The <c>Code</c> of the <see cref="IKeyManagementAlgorithm"/> to use for key management.</param>
    /// <param name="encryptionAlgorithmCode">The <c>Code</c> of the <see cref="IAuthenticatedEncryptionAlgorithm"/> to use for encryption.</param>
    /// <param name="compressionAlgorithmCode">The <c>Code</c> of the <see cref="ICompressionAlgorithm"/> to use for compression.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    /// <typeparam name="T">The type of the payload to encode.</typeparam>
    void EncodeJwe<T>(
        IBufferWriter<char> tokenWriter,
        T payload,
        SecretKey secretKey,
        string keyManagementAlgorithmCode,
        string encryptionAlgorithmCode,
        string? compressionAlgorithmCode,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null);

    /// <summary>
    /// Encrypts a JWE token given the specified payload.
    /// </summary>
    /// <param name="tokenWriter">The destination for the encrypted JWE token.</param>
    /// <param name="payload">The payload to encrypt.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for encryption.</param>
    /// <param name="keyManagementAlgorithmCode">The <c>Code</c> of the <see cref="IKeyManagementAlgorithm"/> to use for key management.</param>
    /// <param name="encryptionAlgorithmCode">The <c>Code</c> of the <see cref="IAuthenticatedEncryptionAlgorithm"/> to use for encryption.</param>
    /// <param name="compressionAlgorithmCode">The <c>Code</c> of the <see cref="ICompressionAlgorithm"/> to use for compression.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    void EncodeJwe(
        IBufferWriter<char> tokenWriter,
        string payload,
        SecretKey secretKey,
        string keyManagementAlgorithmCode,
        string encryptionAlgorithmCode,
        string? compressionAlgorithmCode,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null);

    /// <summary>
    /// Encrypts a JWE token given the specified payload.
    /// </summary>
    /// <param name="payload">The payload to encrypt.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for encryption.</param>
    /// <param name="keyManagementAlgorithmCode">The <c>Code</c> of the <see cref="IKeyManagementAlgorithm"/> to use for key management.</param>
    /// <param name="encryptionAlgorithmCode">The <c>Code</c> of the <see cref="IAuthenticatedEncryptionAlgorithm"/> to use for encryption.</param>
    /// <param name="compressionAlgorithmCode">The <c>Code</c> of the <see cref="ICompressionAlgorithm"/> to use for compression.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    /// <returns>The encrypted JWE token.</returns>
    string EncodeJwe(
        string payload,
        SecretKey secretKey,
        string keyManagementAlgorithmCode,
        string encryptionAlgorithmCode,
        string? compressionAlgorithmCode,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null);

    /// <summary>
    /// Encrypts a JWE token given the specified payload.
    /// </summary>
    /// <param name="tokenWriter">The destination for the encrypted JWE token.</param>
    /// <param name="payload">The payload to encrypt.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for encryption.</param>
    /// <param name="keyManagementAlgorithmCode">The <c>Code</c> of the <see cref="IKeyManagementAlgorithm"/> to use for key management.</param>
    /// <param name="encryptionAlgorithmCode">The <c>Code</c> of the <see cref="IAuthenticatedEncryptionAlgorithm"/> to use for encryption.</param>
    /// <param name="compressionAlgorithmCode">The <c>Code</c> of the <see cref="ICompressionAlgorithm"/> to use for compression.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    void EncodeJwe(
        IBufferWriter<char> tokenWriter,
        ReadOnlySpan<char> payload,
        SecretKey secretKey,
        string keyManagementAlgorithmCode,
        string encryptionAlgorithmCode,
        string? compressionAlgorithmCode,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null);

    /// <summary>
    /// Encrypts a JWE token given the specified payload.
    /// </summary>
    /// <param name="payload">The payload to encrypt.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for encryption.</param>
    /// <param name="keyManagementAlgorithmCode">The <c>Code</c> of the <see cref="IKeyManagementAlgorithm"/> to use for key management.</param>
    /// <param name="encryptionAlgorithmCode">The <c>Code</c> of the <see cref="IAuthenticatedEncryptionAlgorithm"/> to use for encryption.</param>
    /// <param name="compressionAlgorithmCode">The <c>Code</c> of the <see cref="ICompressionAlgorithm"/> to use for compression.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    /// <returns>The encrypted JWE token.</returns>
    string EncodeJwe(
        ReadOnlySpan<char> payload,
        SecretKey secretKey,
        string keyManagementAlgorithmCode,
        string encryptionAlgorithmCode,
        string? compressionAlgorithmCode,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null);

    /// <summary>
    /// Encrypts a JWE token given the specified payload.
    /// </summary>
    /// <param name="payload">The payload to encrypt.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for encryption.</param>
    /// <param name="keyManagementAlgorithmCode">The <c>Code</c> of the <see cref="IKeyManagementAlgorithm"/> to use for key management.</param>
    /// <param name="encryptionAlgorithmCode">The <c>Code</c> of the <see cref="IAuthenticatedEncryptionAlgorithm"/> to use for encryption.</param>
    /// <param name="compressionAlgorithmCode">The <c>Code</c> of the <see cref="ICompressionAlgorithm"/> to use for compression.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    /// <returns>The encrypted JWE token.</returns>
    string EncodeJwe(
        ReadOnlySpan<byte> payload,
        SecretKey secretKey,
        string keyManagementAlgorithmCode,
        string encryptionAlgorithmCode,
        string? compressionAlgorithmCode,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null);

    /// <summary>
    /// Encrypts a JWE token given the specified payload.
    /// </summary>
    /// <param name="tokenWriter">The destination for the encrypted JWE token.</param>
    /// <param name="payload">The payload to encrypt.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for encryption.</param>
    /// <param name="keyManagementAlgorithmCode">The <c>Code</c> of the <see cref="IKeyManagementAlgorithm"/> to use for key management.</param>
    /// <param name="encryptionAlgorithmCode">The <c>Code</c> of the <see cref="IAuthenticatedEncryptionAlgorithm"/> to use for encryption.</param>
    /// <param name="compressionAlgorithmCode">The <c>Code</c> of the <see cref="ICompressionAlgorithm"/> to use for compression.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    void EncodeJwe(
        IBufferWriter<char> tokenWriter,
        ReadOnlySpan<byte> payload,
        SecretKey secretKey,
        string keyManagementAlgorithmCode,
        string encryptionAlgorithmCode,
        string? compressionAlgorithmCode,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null);
}

partial class JoseSerializer
{
    private IKeyManagementAlgorithm GetKeyManagementAlgorithm(string code) =>
        !AlgorithmCollection.TryGetKeyManagementAlgorithm(code, out var algorithm) ?
            throw new JoseInvalidAlgorithmException($"No registered JWA key agreement algorithm for `{code}` was found.") :
            algorithm;

    private IAuthenticatedEncryptionAlgorithm GetAuthenticatedEncryptionAlgorithm(string code) =>
        !AlgorithmCollection.TryGetAuthenticatedEncryptionAlgorithm(code, out var algorithm) ?
            throw new JoseInvalidAlgorithmException($"No registered AEAD encryption algorithm for `{code}` was found.") :
            algorithm;

    private ICompressionAlgorithm GetCompressionAlgorithm(string code) =>
        !AlgorithmCollection.TryGetCompressionAlgorithm(code, out var algorithm) ?
            throw new JoseInvalidAlgorithmException($"No registered JWE compression algorithm for `{code}` was found.") :
            algorithm;

    /// <inheritdoc />
    public string EncodeJwe<T>(
        T payload,
        SecretKey secretKey,
        string keyManagementAlgorithmCode,
        string encryptionAlgorithmCode,
        string? compressionAlgorithmCode,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        using var tokenBuffer = new Sequence<char>();
        using var _ = Serialize(payload, out var payloadBytes);
        EncodeJwe(
            tokenBuffer,
            payloadBytes,
            secretKey,
            keyManagementAlgorithmCode,
            encryptionAlgorithmCode,
            compressionAlgorithmCode,
            extraHeaders);
        return tokenBuffer.AsReadOnlySequence.ToString();
    }

    /// <inheritdoc />
    public void EncodeJwe<T>(
        IBufferWriter<char> tokenWriter,
        T payload,
        SecretKey secretKey,
        string keyManagementAlgorithmCode,
        string encryptionAlgorithmCode,
        string? compressionAlgorithmCode,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        using var _ = Serialize(payload, out var payloadBytes);
        EncodeJwe(
            tokenWriter,
            payloadBytes,
            secretKey,
            keyManagementAlgorithmCode,
            encryptionAlgorithmCode,
            compressionAlgorithmCode,
            extraHeaders);
    }

    /// <inheritdoc />
    public void EncodeJwe(
        IBufferWriter<char> tokenWriter,
        string payload,
        SecretKey secretKey,
        string keyManagementAlgorithmCode,
        string encryptionAlgorithmCode,
        string? compressionAlgorithmCode,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        EncodeJwe(
            tokenWriter,
            payload.AsSpan(),
            secretKey,
            keyManagementAlgorithmCode,
            encryptionAlgorithmCode,
            compressionAlgorithmCode,
            extraHeaders);
    }

    /// <inheritdoc />
    public string EncodeJwe(
        string payload,
        SecretKey secretKey,
        string keyManagementAlgorithmCode,
        string encryptionAlgorithmCode,
        string? compressionAlgorithmCode,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        return EncodeJwe(
            payload.AsSpan(),
            secretKey,
            keyManagementAlgorithmCode,
            encryptionAlgorithmCode,
            compressionAlgorithmCode,
            extraHeaders);
    }

    /// <inheritdoc />
    public void EncodeJwe(
        IBufferWriter<char> tokenWriter,
        ReadOnlySpan<char> payload,
        SecretKey secretKey,
        string keyManagementAlgorithmCode,
        string encryptionAlgorithmCode,
        string? compressionAlgorithmCode,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        var byteCount = Encoding.UTF8.GetByteCount(payload);
        using var payloadLease = CryptoPool.Rent(byteCount, isSensitive: false, out Span<byte> payloadBytes);
        var bytesWritten = Encoding.UTF8.GetBytes(payload, payloadBytes);
        Debug.Assert(bytesWritten == byteCount);
        EncodeJwe(
            tokenWriter,
            payloadBytes,
            secretKey,
            keyManagementAlgorithmCode,
            encryptionAlgorithmCode,
            compressionAlgorithmCode,
            extraHeaders);
    }

    /// <inheritdoc />
    public string EncodeJwe(
        ReadOnlySpan<char> payload,
        SecretKey secretKey,
        string keyManagementAlgorithmCode,
        string encryptionAlgorithmCode,
        string? compressionAlgorithmCode,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        using var tokenBuffer = new Sequence<char>();
        EncodeJwe(
            tokenBuffer,
            payload,
            secretKey,
            keyManagementAlgorithmCode,
            encryptionAlgorithmCode,
            compressionAlgorithmCode,
            extraHeaders);
        return tokenBuffer.AsReadOnlySequence.ToString();
    }

    /// <inheritdoc />
    public string EncodeJwe(
        ReadOnlySpan<byte> payload,
        SecretKey secretKey,
        string keyManagementAlgorithmCode,
        string encryptionAlgorithmCode,
        string? compressionAlgorithmCode,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        using var tokenBuffer = new Sequence<char>();
        EncodeJwe(
            tokenBuffer,
            payload,
            secretKey,
            keyManagementAlgorithmCode,
            encryptionAlgorithmCode,
            compressionAlgorithmCode,
            extraHeaders);
        return tokenBuffer.AsReadOnlySequence.ToString();
    }

    /// <inheritdoc />
    public void EncodeJwe(
        IBufferWriter<char> tokenWriter,
        ReadOnlySpan<byte> payload,
        SecretKey secretKey,
        string keyManagementAlgorithmCode,
        string encryptionAlgorithmCode,
        string? compressionAlgorithmCode,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        var keyManagementAlgorithm = GetKeyManagementAlgorithm(keyManagementAlgorithmCode);
        var encryptionAlgorithm = GetAuthenticatedEncryptionAlgorithm(encryptionAlgorithmCode);
        var compressionAlgorithm = !string.IsNullOrEmpty(compressionAlgorithmCode) ?
            GetCompressionAlgorithm(compressionAlgorithmCode) :
            null;

        var header = extraHeaders != null ?
            new Dictionary<string, object>(extraHeaders) :
            new Dictionary<string, object>();

        header.TryAdd(JoseClaimNames.Header.Typ, JoseConstants.Jwt);
        header[JoseClaimNames.Header.Alg] = keyManagementAlgorithm.Code;
        header[JoseClaimNames.Header.Enc] = encryptionAlgorithm.Code;

        var keyId = secretKey.KeyId;
        if (!string.IsNullOrEmpty(keyId) && JoseOptions.AddKeyIdHeaderDuringEncode)
            header[JoseClaimNames.Header.Kid] = keyId;

        var cekSizeBytes = encryptionAlgorithm.ContentKeySizeBytes;
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

        var nonceSizeBytes = encryptionAlgorithm.NonceSizeBytes;
        var nonce = nonceSizeBytes <= JoseConstants.MaxStackAlloc ?
            stackalloc byte[nonceSizeBytes] :
            GC.AllocateUninitializedArray<byte>(nonceSizeBytes, pinned: false);
        RandomNumberGenerator.Fill(nonce);

        var tagSizeBytes = encryptionAlgorithm.AuthenticationTagSizeBytes;
        var tag = tagSizeBytes <= JoseConstants.MaxStackAlloc ?
            stackalloc byte[tagSizeBytes] :
            GC.AllocateUninitializedArray<byte>(tagSizeBytes, pinned: false);

        // compression must be done before associated data because compression modifies the header
        // and the header is used to derive the associated data
        using var plainTextLease = compressionAlgorithm.Compress(header, payload, out var plainText);
        using var headerLease = EncodeJose(b64: true, header, out var encodedHeader);
        using var aadLease = Encode(Encoding.ASCII, encodedHeader, out var aad);

        var cipherTextSizeBytes = encryptionAlgorithm.GetCipherTextSizeBytes(plainText.Length);
        using var cipherTextLease = CryptoPool.Rent(cipherTextSizeBytes, isSensitive: false, out Span<byte> cipherText);
        encryptionAlgorithm.Encrypt(cek, nonce, plainText, aad, cipherText, tag);

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
        Debug.Assert(compactJwt.ProtectionType == JoseConstants.Jwe);

        // JWE Protected Header
        var jweProtectedHeader = compactJwt.Segments.First;
        var header = compactJwt.DeserializedHeader;

        // JWE Encrypted Key
        var jweEncryptedKey = jweProtectedHeader.Next!;
        var encodedEncryptedKey = jweEncryptedKey.Memory.Span;
        using var encryptedKeyLease = DecodeBase64Url(
            "JWE Encrypted Key",
            encodedEncryptedKey,
            isSensitive: true,
            out var encryptedKeyBytes);

        // JWE Initialization Vector
        var jweInitializationVector = jweEncryptedKey.Next!;
        var encodedInitializationVector = jweInitializationVector.Memory.Span;
        using var initializationVectorLease = DecodeBase64Url(
            "JWE Initialization Vector",
            encodedInitializationVector,
            isSensitive: false,
            out var initializationVectorBytes);

        // JWE Ciphertext
        var jweCiphertext = jweInitializationVector.Next!;
        var encodedCiphertext = jweCiphertext.Memory.Span;
        using var cipherTextLease = DecodeBase64Url(
            "JWE Ciphertext",
            encodedCiphertext,
            isSensitive: false,
            out var cipherTextBytes);

        // JWE Authentication Tag
        var jweAuthenticationTag = jweCiphertext.Next!;
        var encodedAuthenticationTag = jweAuthenticationTag.Memory.Span;
        using var authenticationTagLease = DecodeBase64Url(
            "JWE Authentication Tag",
            encodedAuthenticationTag,
            isSensitive: false,
            out var authenticationTagBytes);

        if (!header.TryGetPropertyValue<string>(JoseClaimNames.Header.Alg, out var keyManagementAlgorithmCode))
            throw new JoseException("The JWE header is missing the 'alg' field.");

        if (!header.TryGetPropertyValue<string>(JoseClaimNames.Header.Enc, out var encryptionAlgorithmCode))
            throw new JoseException("The JWE header is missing the 'enc' field.");

        var keyManagementAlgorithm = GetKeyManagementAlgorithm(keyManagementAlgorithmCode);
        var encryptionAlgorithm = GetAuthenticatedEncryptionAlgorithm(encryptionAlgorithmCode);

        var cekSizeBytes = encryptionAlgorithm.ContentKeySizeBytes;
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

        var plainTextSizeBytes = encryptionAlgorithm.GetMaxPlainTextSizeBytes(cipherTextBytes.Length);
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

        var decryptResult = encryptionAlgorithm.TryDecrypt(
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
