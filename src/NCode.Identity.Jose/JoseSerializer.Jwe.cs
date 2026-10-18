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
using NCode.Buffers;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Algorithms.Compression;
using NCode.Identity.Jose.Encoders;
using NCode.Identity.Jose.Exceptions;
using NCode.Identity.Secrets.Keys;
using NCode.Json;
using Nerdbank.Streams;

namespace NCode.Identity.Jose;

partial class JoseSerializer
{
    private KeyManagementAlgorithm GetKeyManagementAlgorithm(string code) =>
        !AlgorithmCollection.TryGetKeyManagementAlgorithm(code, out var algorithm)
            ? throw new JoseInvalidAlgorithmException(
                $"The `{code}` algorithm is not supported for key management."
            )
            : AssertEnabled(algorithm);

    private AuthenticatedEncryptionAlgorithm GetAuthenticatedEncryptionAlgorithm(string code) =>
        !AlgorithmCollection.TryGetAuthenticatedEncryptionAlgorithm(code, out var algorithm)
            ? throw new JoseInvalidAlgorithmException(
                $"The `{code}` algorithm is not supported for AEAD encryption."
            )
            : AssertEnabled(algorithm);

    private CompressionAlgorithm GetCompressionAlgorithm(string code) =>
        !AlgorithmCollection.TryGetCompressionAlgorithm(code, out var algorithm)
            ? throw new JoseInvalidAlgorithmException(
                $"The `{code}` algorithm is not supported for compression."
            )
            : AssertEnabled(algorithm);

    /// <inheritdoc />
    public JoseEncoder CreateEncoder(JoseEncryptionOptions encryptionOptions) =>
        new JoseEncryptionEncoder(this, encryptionOptions);

    internal void Encode(
        IBufferWriter<char> tokenWriter,
        ReadOnlySpan<byte> payload,
        JoseEncryptionOptions encryptionOptions
    )
    {
        if (encryptionOptions.Recipients.Count == 0)
            throw new JoseException("At least one recipient is required to encode a JWE token.");

        switch (encryptionOptions.Format)
        {
            case JoseSerializationFormat.Compact:
                EncodeCompactJwe(tokenWriter, payload, encryptionOptions);
                break;

            case JoseSerializationFormat.Flattened:
            case JoseSerializationFormat.General:
                EncodeJsonJwe(tokenWriter, payload, encryptionOptions);
                break;

            default:
                throw new JoseException(
                    $"The '{encryptionOptions.Format}' serialization format is not supported for JWE tokens."
                );
        }
    }

    private void EncodeCompactJwe(
        IBufferWriter<char> tokenWriter,
        ReadOnlySpan<byte> payload,
        JoseEncryptionOptions encryptionOptions
    )
    {
        if (encryptionOptions.Recipients.Count != 1)
            throw new JoseException(
                "The compact JWE serialization requires exactly one recipient."
            );

        if (encryptionOptions.SharedUnprotectedHeaders is not null)
            throw new JoseException(
                "The compact JWE serialization cannot carry a shared unprotected header."
            );

        if (encryptionOptions.AdditionalAuthenticatedData is not null)
            throw new JoseException(
                "The compact JWE serialization cannot carry additional authenticated data (AAD)."
            );

        var recipient = encryptionOptions.Recipients.First();
        var (secretKey, keyManagementAlgorithm) = recipient.KeyManagementCredentials;
        var authenticatedEncryptionAlgorithm = encryptionOptions.EncryptionAlgorithm;
        var compressionAlgorithm = encryptionOptions.CompressionAlgorithm;

        // the compact serialization has only one header, so the shared protected header and the single
        // recipient's key-management header are folded together
        var header = BuildJweProtectedHeader(encryptionOptions);
        MergeHeaders(header, recipient.Headers);
        header[JoseClaimNames.Header.Alg] = keyManagementAlgorithm.Code;

        var keyId = secretKey.KeyId;
        if (!string.IsNullOrEmpty(keyId) && recipient.AddKeyIdHeader)
            header[JoseClaimNames.Header.Kid] = keyId;

        var cekSizeBytes = authenticatedEncryptionAlgorithm.ContentKeySizeBytes;
        using var cekLease = BufferFactory.Rent(
            cekSizeBytes,
            isSensitive: true,
            out Span<byte> cek
        );

        using var encryptedCekBuffer = BufferFactory.CreatePooledBufferWriter(isSensitive: false);

        keyManagementAlgorithm.WrapNewKey(secretKey, header, cek, encryptedCekBuffer);

        var nonceSizeBytes = authenticatedEncryptionAlgorithm.NonceSizeBytes;
        var nonce =
            nonceSizeBytes <= JoseConstants.MaxStackAlloc
                ? stackalloc byte[nonceSizeBytes]
                : GC.AllocateUninitializedArray<byte>(nonceSizeBytes, pinned: false);

        RandomNumberGenerator.Fill(nonce);

        var tagSizeBytes = authenticatedEncryptionAlgorithm.AuthenticationTagSizeBytes;
        var tag =
            tagSizeBytes <= JoseConstants.MaxStackAlloc
                ? stackalloc byte[tagSizeBytes]
                : GC.AllocateUninitializedArray<byte>(tagSizeBytes, pinned: false);

        // compression must be done before associated data because compression modifies the header
        // and the header is used to derive the associated data
        using var plainTextLease = compressionAlgorithm.Compress(
            header,
            payload,
            out var plainText
        );
        using var headerLease = EncodeJose(b64: true, header, out var encodedHeader);
        using var aadLease = Encode(Encoding.ASCII, encodedHeader, out var aad);

        var cipherTextSizeBytes = authenticatedEncryptionAlgorithm.GetCipherTextSizeBytes(
            plainText.Length
        );
        using var cipherTextLease = BufferFactory.Rent(
            cipherTextSizeBytes,
            isSensitive: false,
            out Span<byte> cipherText
        );
        authenticatedEncryptionAlgorithm.Encrypt(cek, nonce, plainText, aad, cipherText, tag);

        // BASE64URL(UTF8(JWE Protected Header)) || '.' ||
        WriteCompactSegment(encodedHeader, tokenWriter);

        // BASE64URL(JWE Encrypted Key) || '.' ||
        WriteCompactSegment(encryptedCekBuffer.AsReadOnlySequence, tokenWriter);

        // BASE64URL(JWE Initialization Vector) || '.' ||
        WriteCompactSegment(nonce, tokenWriter);

        // BASE64URL(JWE Ciphertext) || '.' ||
        WriteCompactSegment(cipherText, tokenWriter);

        // BASE64URL(JWE Authentication Tag)
        WriteCompactSegment(tag, tokenWriter, addDot: false);
    }

    private static Dictionary<string, object> BuildJweProtectedHeader(
        JoseEncryptionOptions encryptionOptions
    )
    {
        var protectedHeaders = encryptionOptions.ProtectedHeaders;
        var header =
            protectedHeaders != null
                ? new Dictionary<string, object>(protectedHeaders)
                : new Dictionary<string, object>();

        header[JoseClaimNames.Header.Enc] = encryptionOptions.EncryptionAlgorithm.Code;

        var tokenType = encryptionOptions.TokenType;
        if (!string.IsNullOrEmpty(tokenType))
            header[JoseClaimNames.Header.Typ] = tokenType;

        return header;
    }

    private static void MergeHeaders(
        Dictionary<string, object> target,
        IEnumerable<KeyValuePair<string, object>>? source
    )
    {
        if (source is null)
            return;

        foreach (var (key, value) in source)
            target[key] = value;
    }

    private void DecryptJwe(
        CompactJwt compactJwt,
        SecretKey secretKey,
        IBufferWriter<byte> payloadBytes
    )
    {
        /*
              BASE64URL(UTF8(JWE Protected Header)) || '.' ||
              BASE64URL(JWE Encrypted Key) || '.' ||
              BASE64URL(JWE Initialization Vector) || '.' ||
              BASE64URL(JWE Ciphertext) || '.' ||
              BASE64URL(JWE Authentication Tag)
        */
        Debug.Assert(compactJwt.ProtectionType == JoseProtectionTypes.Jwe);

        var segments = compactJwt.Segments;
        if (segments.Count != JweSegmentCount)
            throw new InvalidOperationException("Malformed JWE token.");

        var header = compactJwt.Header;

        // JWE Protected Header
        var encodedProtectedHeader = segments[0].Span;

        // JWE Encrypted Key
        using var encryptedKeyLease = DecodeBase64Url(
            segments[1].Span,
            isSensitive: true,
            out var encryptedKeyBytes
        );

        // JWE Initialization Vector
        using var initializationVectorLease = DecodeBase64Url(
            segments[2].Span,
            isSensitive: false,
            out var initializationVectorBytes
        );

        // JWE Ciphertext
        using var cipherTextLease = DecodeBase64Url(
            segments[3].Span,
            isSensitive: false,
            out var cipherTextBytes
        );

        // JWE Authentication Tag
        using var authenticationTagLease = DecodeBase64Url(
            segments[4].Span,
            isSensitive: false,
            out var authenticationTagBytes
        );

        if (
            !header.TryGetPropertyValue<string>(
                JoseClaimNames.Header.Alg,
                out var keyManagementAlgorithmCode
            )
        )
            throw new JoseException("The JWE header is missing the 'alg' field.");

        if (
            !header.TryGetPropertyValue<string>(
                JoseClaimNames.Header.Enc,
                out var authenticatedEncryptionAlgorithmCode
            )
        )
            throw new JoseException("The JWE header is missing the 'enc' field.");

        var keyManagementAlgorithm = GetKeyManagementAlgorithm(keyManagementAlgorithmCode);
        var authenticatedEncryptionAlgorithm = GetAuthenticatedEncryptionAlgorithm(
            authenticatedEncryptionAlgorithmCode
        );

        var cekSizeBytes = authenticatedEncryptionAlgorithm.ContentKeySizeBytes;
        using var contentKeyLease = BufferFactory.Rent(
            cekSizeBytes,
            isSensitive: true,
            out Span<byte> contentKey
        );

        var unwrapResult = keyManagementAlgorithm.TryUnwrapKey(
            secretKey,
            header,
            encryptedKeyBytes,
            contentKey,
            out var unwrapBytesWritten
        );

        if (!unwrapResult || unwrapBytesWritten == 0)
            throw new JoseEncryptionException(
                "Failed to decrypt the encrypted content encryption key (CEK)."
            );

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

        using var associatedDataLease = Encode(
            Encoding.ASCII,
            encodedProtectedHeader,
            out var associatedDataBytes
        );

        var plainTextSizeBytes = authenticatedEncryptionAlgorithm.GetMaxPlainTextSizeBytes(
            cipherTextBytes.Length
        );
        using var plainTextLease = BufferFactory.Rent(
            plainTextSizeBytes,
            isSensitive: false,
            out Span<byte> plainTextBytes
        );

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
            out var decryptBytesWritten
        );

        if (!decryptResult || decryptBytesWritten == 0)
            throw new JoseEncryptionException("Failed to decrypt the JWE Ciphertext.");

        if (decryptBytesWritten < plainTextSizeBytes)
            plainTextBytes = plainTextBytes[..decryptBytesWritten];

        /*
           17.  If a "zip" parameter was included, uncompress the decrypted
                plaintext using the specified compression algorithm.
        */

        if (
            header.TryGetPropertyValue<string>(
                JoseClaimNames.Header.Zip,
                out var compressionAlgorithmCode
            )
        )
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
