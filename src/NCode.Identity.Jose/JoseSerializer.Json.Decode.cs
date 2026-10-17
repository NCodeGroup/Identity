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
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NCode.Buffers;
using NCode.Encoders;
using NCode.Identity.Jose.Exceptions;
using NCode.Identity.Secrets.Keys;
using NCode.Json;

namespace NCode.Identity.Jose;

partial class JoseSerializer
{
    private static bool IsJsonSerialized(string token)
    {
        foreach (var ch in token)
        {
            if (char.IsWhiteSpace(ch))
                continue;

            return ch == '{';
        }

        return false;
    }

    private string DecodeJson(string token, SecretKey secretKey, out JsonElement header)
    {
        using var payloadBuffer = BufferFactory.CreatePooledBufferWriter(isSensitive: false);
        DecodeJson(token, secretKey, payloadBuffer, out header);
        return DecodeUtf8(payloadBuffer.AsReadOnlySequence);
    }

    private T? DeserializeJson<T>(string token, SecretKey secretKey, out JsonElement header)
    {
        using var payloadBuffer = BufferFactory.CreatePooledBufferWriter(isSensitive: false);
        DecodeJson(token, secretKey, payloadBuffer, out header);
        return Deserialize<T>(payloadBuffer.AsReadOnlySequence);
    }

    private void DecodeJson(
        string token,
        SecretKey secretKey,
        IBufferWriter<byte> payloadWriter,
        out JsonElement header
    )
    {
        using var document = JsonDocument.Parse(token);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
            throw new JoseException("The JOSE JSON serialization must be a JSON object.");

        if (root.TryGetProperty(JoseClaimNames.Json.Ciphertext, out _))
        {
            DecryptJsonJwe(root, secretKey, payloadWriter, out header);
        }
        else if (
            root.TryGetProperty(JoseClaimNames.Json.Signatures, out _)
            || root.TryGetProperty(JoseClaimNames.Json.Signature, out _)
        )
        {
            VerifyJsonJws(root, secretKey, payloadWriter, out header);
        }
        else
        {
            throw new JoseException(
                "The JOSE JSON serialization is neither a recognized JWS nor JWE."
            );
        }
    }

    private static JsonElement MergeHeaders(params JsonElement[] sources)
    {
        var merged = new JsonObject();

        foreach (var source in sources)
        {
            if (source.ValueKind != JsonValueKind.Object)
                continue;

            foreach (var property in source.EnumerateObject())
                merged[property.Name] = JsonNode.Parse(property.Value.GetRawText());
        }

        return JsonSerializer.SerializeToElement(merged);
    }

    private static string GetRequiredJsonString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
            throw new JoseException(
                $"The JOSE JSON serialization is missing the required '{name}' member."
            );

        return value.GetString() ?? string.Empty;
    }

    private void VerifyJsonJws(
        JsonElement root,
        SecretKey secretKey,
        IBufferWriter<byte> payloadWriter,
        out JsonElement header
    )
    {
        if (
            !root.TryGetProperty(JoseClaimNames.Json.Payload, out var payloadElement)
            || payloadElement.ValueKind != JsonValueKind.String
        )
            throw new JoseException(
                "The JWS JSON serialization is missing the 'payload' member; a detached payload must be "
                    + "verified via VerifyJws."
            );

        var encodedPayload = payloadElement.GetString() ?? string.Empty;

        var signatures = new List<JsonElement>();
        if (root.TryGetProperty(JoseClaimNames.Json.Signatures, out var signaturesElement))
        {
            if (signaturesElement.ValueKind != JsonValueKind.Array)
                throw new JoseException("The 'signatures' member must be an array.");

            signatures.AddRange(signaturesElement.EnumerateArray());
        }
        else
        {
            signatures.Add(root);
        }

        List<Exception>? failures = null;
        foreach (var signature in signatures)
        {
            var encodedProtected = GetRequiredJsonString(signature, JoseClaimNames.Json.Protected);
            var protectedHeader = DeserializeHeader(encodedProtected);

            var unprotectedHeader = signature.TryGetProperty(
                JoseClaimNames.Json.Header,
                out var unprotectedElement
            )
                ? unprotectedElement
                : default;

            var mergedHeader = MergeHeaders(protectedHeader, unprotectedHeader);

            var encodedSignature = GetRequiredJsonString(signature, JoseClaimNames.Json.Signature);

            try
            {
                VerifyJws(
                    secretKey,
                    mergedHeader,
                    encodedProtected,
                    encodedPayload,
                    encodedSignature
                );
            }
            catch (Exception exception)
            {
                (failures ??= []).Add(exception);
                continue;
            }

            if (!mergedHeader.TryGetPropertyValue<bool>(JoseClaimNames.Header.B64, out var b64))
                b64 = true;

            if (b64)
                Base64Url.Decode(encodedPayload, payloadWriter);
            else
                SecureEncoding.UTF8.GetBytes(encodedPayload, payloadWriter);

            header = mergedHeader;
            return;
        }

        throw new JoseIntegrityException(
            "None of the JWS signatures could be verified with the provided key.",
            AsInnerException(failures)
        );
    }

    private void DecryptJsonJwe(
        JsonElement root,
        SecretKey secretKey,
        IBufferWriter<byte> payloadWriter,
        out JsonElement header
    )
    {
        var encodedProtected = root.TryGetProperty(
            JoseClaimNames.Json.Protected,
            out var protectedElement
        )
            ? protectedElement.GetString() ?? string.Empty
            : string.Empty;

        var protectedHeader =
            encodedProtected.Length > 0 ? DeserializeHeader(encodedProtected) : default;

        var sharedUnprotected = root.TryGetProperty(
            JoseClaimNames.Json.Unprotected,
            out var sharedUnprotectedElement
        )
            ? sharedUnprotectedElement
            : default;

        var encodedIv = GetRequiredJsonString(root, JoseClaimNames.Json.Iv);
        var encodedCiphertext = GetRequiredJsonString(root, JoseClaimNames.Json.Ciphertext);
        var encodedTag = GetRequiredJsonString(root, JoseClaimNames.Json.Tag);

        var encodedAad = root.TryGetProperty(JoseClaimNames.Json.Aad, out var aadElement)
            ? aadElement.GetString()
            : null;

        var recipients = new List<JsonElement>();
        if (root.TryGetProperty(JoseClaimNames.Json.Recipients, out var recipientsElement))
        {
            if (recipientsElement.ValueKind != JsonValueKind.Array)
                throw new JoseException("The 'recipients' member must be an array.");

            recipients.AddRange(recipientsElement.EnumerateArray());
        }
        else
        {
            recipients.Add(root);
        }

        List<Exception>? failures = null;
        foreach (var recipient in recipients)
        {
            var recipientHeader = recipient.TryGetProperty(
                JoseClaimNames.Json.Header,
                out var recipientHeaderElement
            )
                ? recipientHeaderElement
                : default;

            var mergedHeader = MergeHeaders(protectedHeader, sharedUnprotected, recipientHeader);

            var encodedEncryptedKey = recipient.TryGetProperty(
                JoseClaimNames.Json.EncryptedKey,
                out var encryptedKeyElement
            )
                ? encryptedKeyElement.GetString() ?? string.Empty
                : string.Empty;

            try
            {
                DecryptJsonJweRecipient(
                    secretKey,
                    mergedHeader,
                    encodedProtected,
                    encodedAad,
                    encodedEncryptedKey,
                    encodedIv,
                    encodedCiphertext,
                    encodedTag,
                    payloadWriter
                );
            }
            catch (Exception exception)
            {
                (failures ??= []).Add(exception);
                continue;
            }

            header = mergedHeader;
            return;
        }

        throw new JoseEncryptionException(
            "None of the JWE recipients could be decrypted with the provided key.",
            AsInnerException(failures)
        );
    }

    private static Exception AsInnerException(List<Exception>? failures) =>
        failures switch
        {
            null or { Count: 0 } => new JoseException("No candidates were available."),
            { Count: 1 } => failures[0],
            _ => new AggregateException(failures),
        };

    private void DecryptJsonJweRecipient(
        SecretKey secretKey,
        JsonElement header,
        ReadOnlySpan<char> encodedProtected,
        string? encodedAad,
        ReadOnlySpan<char> encodedEncryptedKey,
        ReadOnlySpan<char> encodedIv,
        ReadOnlySpan<char> encodedCiphertext,
        ReadOnlySpan<char> encodedTag,
        IBufferWriter<byte> payloadWriter
    )
    {
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

        using var encryptedKeyLease = DecodeBase64Url(
            encodedEncryptedKey,
            isSensitive: true,
            out var encryptedKeyBytes
        );
        using var ivLease = DecodeBase64Url(encodedIv, isSensitive: false, out var ivBytes);
        using var cipherTextLease = DecodeBase64Url(
            encodedCiphertext,
            isSensitive: false,
            out var cipherTextBytes
        );
        using var tagLease = DecodeBase64Url(encodedTag, isSensitive: false, out var tagBytes);

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

        using var associatedDataLease = BuildJsonJweAssociatedData(
            encodedProtected,
            encodedAad,
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

        var decryptResult = authenticatedEncryptionAlgorithm.TryDecrypt(
            contentKey,
            ivBytes,
            cipherTextBytes,
            associatedDataBytes,
            tagBytes,
            plainTextBytes,
            out var decryptBytesWritten
        );

        if (!decryptResult || decryptBytesWritten == 0)
            throw new JoseEncryptionException("Failed to decrypt the JWE Ciphertext.");

        if (decryptBytesWritten < plainTextSizeBytes)
            plainTextBytes = plainTextBytes[..decryptBytesWritten];

        if (
            header.TryGetPropertyValue<string>(
                JoseClaimNames.Header.Zip,
                out var compressionAlgorithmCode
            )
        )
        {
            var compressionAlgorithm = GetCompressionAlgorithm(compressionAlgorithmCode);
            compressionAlgorithm.Decompress(plainTextBytes, payloadWriter);
        }
        else
        {
            payloadWriter.Write(plainTextBytes);
        }
    }

    private static IDisposable BuildJsonJweAssociatedData(
        ReadOnlySpan<char> encodedProtected,
        string? encodedAad,
        out ReadOnlySpan<byte> associatedData
    )
    {
        if (encodedAad is null)
            return Encode(Encoding.ASCII, encodedProtected, out associatedData);

        var combined = string.Concat(encodedProtected, ".", encodedAad);
        return Encode(Encoding.ASCII, combined.AsSpan(), out associatedData);
    }
}
