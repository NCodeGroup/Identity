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
using NCode.Buffers;
using NCode.Encoders;
using NCode.Identity.Jose.Algorithms.Compression;
using NCode.Identity.Jose.Exceptions;

namespace NCode.Identity.Jose;

partial class JoseSerializer
{
    private void EncodeJsonJwe(
        IBufferWriter<char> tokenWriter,
        ReadOnlySpan<byte> payload,
        JoseEncryptionOptions encryptionOptions
    )
    {
        /*
           JWE JSON Serialization (RFC 7516 §7.2)

           General:
           {
             "protected": "<b64>",
             "unprotected": { ... },
             "recipients": [ { "header": { ... }, "encrypted_key": "<b64>" }, ... ],
             "aad": "<b64>",
             "iv": "<b64>",
             "ciphertext": "<b64>",
             "tag": "<b64>"
           }

           Flattened:
           {
             "protected": "<b64>",
             "unprotected": { ... },
             "header": { ... },
             "encrypted_key": "<b64>",
             "aad": "<b64>",
             "iv": "<b64>",
             "ciphertext": "<b64>",
             "tag": "<b64>"
           }
        */

        var format = encryptionOptions.Format;
        var recipients = encryptionOptions.Recipients;

        if (format == JoseSerializationFormat.Flattened && recipients.Count != 1)
            throw new JoseException(
                "The flattened JWE JSON serialization requires exactly one recipient."
            );

        var authenticatedEncryptionAlgorithm = encryptionOptions.EncryptionAlgorithm;
        var compressionAlgorithm = encryptionOptions.CompressionAlgorithm;
        var isMultiRecipient = recipients.Count > 1;

        // the zip header is added to this shared protected header by the compression step below
        var protectedHeader = BuildJweProtectedHeader(encryptionOptions);

        var cekSizeBytes = authenticatedEncryptionAlgorithm.ContentKeySizeBytes;
        using var cekLease = BufferFactory.Rent(
            cekSizeBytes,
            isSensitive: true,
            out Span<byte> cek
        );

        var recipientHeaders = new List<Dictionary<string, object>>(recipients.Count);
        var encodedEncryptedKeys = new List<string>(recipients.Count);

        if (isMultiRecipient)
        {
            // all recipients share a single content-encryption key (CEK), generated once here; each recipient
            // wraps that same CEK with its own key-management algorithm
            RandomNumberGenerator.Fill(cek);

            foreach (var recipient in recipients)
            {
                var (secretKey, keyManagementAlgorithm) = recipient.KeyManagementCredentials;

                if (!keyManagementAlgorithm.SupportsKeyWrapping)
                    throw new JoseException(
                        $"The '{keyManagementAlgorithm.Code}' key management algorithm derives the content "
                            + "encryption key directly and cannot be used with multiple recipients."
                    );

                var recipientHeader = BuildJweRecipientHeader(
                    recipient,
                    keyManagementAlgorithm.Code,
                    secretKey.KeyId
                );

                using var encryptedKeyBuffer = BufferFactory.CreatePooledBufferWriter(
                    isSensitive: false
                );
                keyManagementAlgorithm.WrapKey(secretKey, recipientHeader, cek, encryptedKeyBuffer);

                encodedEncryptedKeys.Add(ToBase64UrlString(encryptedKeyBuffer.AsReadOnlySequence));
                recipientHeaders.Add(recipientHeader);
            }
        }
        else
        {
            var recipient = recipients.First();
            var (secretKey, keyManagementAlgorithm) = recipient.KeyManagementCredentials;

            var recipientHeader = BuildJweRecipientHeader(
                recipient,
                keyManagementAlgorithm.Code,
                secretKey.KeyId
            );

            // direct key-agreement algorithms (dir, ECDH-ES) read 'enc' from the header; it is removed afterwards
            // because 'enc' belongs in the shared protected header on the wire
            recipientHeader[JoseClaimNames.Header.Enc] = authenticatedEncryptionAlgorithm.Code;

            using var encryptedKeyBuffer = BufferFactory.CreatePooledBufferWriter(
                isSensitive: false
            );
            keyManagementAlgorithm.WrapNewKey(secretKey, recipientHeader, cek, encryptedKeyBuffer);

            recipientHeader.Remove(JoseClaimNames.Header.Enc);

            encodedEncryptedKeys.Add(ToBase64UrlString(encryptedKeyBuffer.AsReadOnlySequence));
            recipientHeaders.Add(recipientHeader);
        }

        // compression modifies the shared protected header (adds 'zip'), so it must run before the header is encoded
        using var plainTextLease = compressionAlgorithm.Compress(
            protectedHeader,
            payload,
            out var plainText
        );

        using var protectedLease = EncodeJose(b64: true, protectedHeader, out var encodedProtected);

        using var aadInputLease = BuildJweAssociatedData(
            encodedProtected,
            encryptionOptions.AdditionalAuthenticatedData,
            out var associatedData,
            out var encodedAad
        );

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

        var cipherTextSizeBytes = authenticatedEncryptionAlgorithm.GetCipherTextSizeBytes(
            plainText.Length
        );
        using var cipherTextLease = BufferFactory.Rent(
            cipherTextSizeBytes,
            isSensitive: false,
            out Span<byte> cipherText
        );
        authenticatedEncryptionAlgorithm.Encrypt(
            cek,
            nonce,
            plainText,
            associatedData,
            cipherText,
            tag
        );

        var encodedIv = Base64Url.Encode(nonce);
        var encodedTag = Base64Url.Encode(tag);
        var encodedCiphertext = Base64Url.Encode((ReadOnlySpan<byte>)cipherText);

        using var jsonBuffer = BufferFactory.CreatePooledBufferWriter(isSensitive: false, 1024);

        using (var writer = new Utf8JsonWriter(jsonBuffer))
        {
            writer.WriteStartObject();

            writer.WriteString(JoseClaimNames.Json.Protected, encodedProtected);

            if (encryptionOptions.SharedUnprotectedHeaders is not null)
            {
                writer.WritePropertyName(JoseClaimNames.Json.Unprotected);
                WriteJsonHeaderObject(writer, encryptionOptions.SharedUnprotectedHeaders);
            }

            if (format == JoseSerializationFormat.General)
            {
                writer.WritePropertyName(JoseClaimNames.Json.Recipients);
                writer.WriteStartArray();

                for (var i = 0; i < recipientHeaders.Count; i++)
                {
                    writer.WriteStartObject();
                    WriteJweRecipientHeaderMember(writer, recipientHeaders[i]);
                    writer.WriteString(JoseClaimNames.Json.EncryptedKey, encodedEncryptedKeys[i]);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }
            else
            {
                WriteJweRecipientHeaderMember(writer, recipientHeaders[0]);
                writer.WriteString(JoseClaimNames.Json.EncryptedKey, encodedEncryptedKeys[0]);
            }

            if (encodedAad is not null)
                writer.WriteString(JoseClaimNames.Json.Aad, encodedAad);

            writer.WriteString(JoseClaimNames.Json.Iv, encodedIv);
            writer.WriteString(JoseClaimNames.Json.Ciphertext, encodedCiphertext);
            writer.WriteString(JoseClaimNames.Json.Tag, encodedTag);

            writer.WriteEndObject();
        }

        EncodeJose(b64: false, jsonBuffer.AsReadOnlySequence, tokenWriter);
    }

    private static Dictionary<string, object> BuildJweRecipientHeader(
        JoseRecipientOptions recipient,
        string algorithmCode,
        string? keyId
    )
    {
        var headers = recipient.Headers;
        var header =
            headers != null
                ? new Dictionary<string, object>(headers)
                : new Dictionary<string, object>();

        header[JoseClaimNames.Header.Alg] = algorithmCode;

        if (!string.IsNullOrEmpty(keyId) && recipient.AddKeyIdHeader)
            header[JoseClaimNames.Header.Kid] = keyId;

        return header;
    }

    private void WriteJweRecipientHeaderMember(
        Utf8JsonWriter writer,
        Dictionary<string, object> recipientHeader
    )
    {
        if (recipientHeader.Count == 0)
            return;

        writer.WritePropertyName(JoseClaimNames.Json.Header);
        WriteJsonHeaderObject(writer, recipientHeader);
    }

    private static IDisposable BuildJweAssociatedData(
        ReadOnlySpan<char> encodedProtected,
        ReadOnlyMemory<byte>? additionalAuthenticatedData,
        out ReadOnlySpan<byte> associatedData,
        out string? encodedAad
    )
    {
        if (additionalAuthenticatedData is null)
        {
            encodedAad = null;
            return Encode(SecureEncoding.ASCII, encodedProtected, out associatedData);
        }

        encodedAad = Base64Url.Encode(additionalAuthenticatedData.Value.Span);
        var combined = string.Concat(encodedProtected, ".", encodedAad);
        return Encode(SecureEncoding.ASCII, combined.AsSpan(), out associatedData);
    }

    private static string ToBase64UrlString(ReadOnlySequence<byte> data)
    {
        if (data.IsEmpty)
            return string.Empty;

        if (data.IsSingleSegment)
            return Base64Url.Encode(data.FirstSpan);

        var array = data.ToArray();
        return Base64Url.Encode(array);
    }
}
