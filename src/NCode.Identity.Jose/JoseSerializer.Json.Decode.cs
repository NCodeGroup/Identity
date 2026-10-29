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

    private static JsonJwt ParseJson(string token)
    {
        var root = JsonElement.Parse(token);

        if (root.ValueKind != JsonValueKind.Object)
            throw new JoseException("The JOSE JSON serialization must be a JSON object.");

        if (root.TryGetProperty(JoseClaimNames.Json.Ciphertext, out _))
        {
            var format = root.TryGetProperty(JoseClaimNames.Json.Recipients, out _)
                ? JoseSerializationFormat.General
                : JoseSerializationFormat.Flattened;
            return new JsonJwt(
                JoseProtectionTypes.Jwe,
                format,
                token,
                root,
                ComputeJweHeader(root)
            );
        }

        if (root.TryGetProperty(JoseClaimNames.Json.Signatures, out _))
            return new JsonJwt(
                JoseProtectionTypes.Jws,
                JoseSerializationFormat.General,
                token,
                root,
                ComputeJwsHeader(root)
            );

        if (root.TryGetProperty(JoseClaimNames.Json.Signature, out _))
            return new JsonJwt(
                JoseProtectionTypes.Jws,
                JoseSerializationFormat.Flattened,
                token,
                root,
                ComputeJwsHeader(root)
            );

        throw new JoseException("The JOSE JSON serialization is neither a recognized JWS nor JWE.");
    }

    private static JsonElement ComputeJwsHeader(JsonElement root)
    {
        var signature = root;
        if (
            root.TryGetProperty(JoseClaimNames.Json.Signatures, out var signatures)
            && signatures.ValueKind == JsonValueKind.Array
        )
        {
            signature = default;
            foreach (var element in signatures.EnumerateArray())
            {
                signature = element;
                break;
            }
        }

        var protectedHeader =
            signature.ValueKind == JsonValueKind.Object
            && signature.TryGetPropertyValue<string>(
                JoseClaimNames.Json.Protected,
                out var encodedProtected
            )
                ? DeserializeHeader(encodedProtected)
                : default;

        var unprotectedHeader =
            signature.ValueKind == JsonValueKind.Object
            && signature.TryGetProperty(JoseClaimNames.Json.Header, out var unprotectedElement)
                ? unprotectedElement
                : default;

        return MergeHeaders(protectedHeader, unprotectedHeader);
    }

    private static JsonElement ComputeJweHeader(JsonElement root)
    {
        var encodedProtected = root.TryGetPropertyValue<string>(
            JoseClaimNames.Json.Protected,
            out var protectedValue
        )
            ? protectedValue
            : string.Empty;
        var protectedHeader =
            encodedProtected.Length > 0 ? DeserializeHeader(encodedProtected) : default;

        var sharedUnprotected = root.TryGetProperty(
            JoseClaimNames.Json.Unprotected,
            out var sharedElement
        )
            ? sharedElement
            : default;

        var recipientHeader = default(JsonElement);
        if (
            root.TryGetProperty(JoseClaimNames.Json.Recipients, out var recipients)
            && recipients.ValueKind == JsonValueKind.Array
        )
        {
            foreach (var recipient in recipients.EnumerateArray())
            {
                if (
                    recipient.TryGetProperty(
                        JoseClaimNames.Json.Header,
                        out var recipientHeaderElement
                    )
                )
                    recipientHeader = recipientHeaderElement;
                break;
            }
        }
        else if (root.TryGetProperty(JoseClaimNames.Json.Header, out var flatHeader))
        {
            recipientHeader = flatHeader;
        }

        return MergeHeaders(protectedHeader, sharedUnprotected, recipientHeader);
    }

    private void DecodeJson(JsonJwt jsonJwt, SecretKey secretKey, IBufferWriter<byte> payloadWriter)
    {
        switch (jsonJwt.ProtectionType)
        {
            case JoseProtectionTypes.Jwe:
                DecryptJsonJwe(jsonJwt.Root, secretKey, payloadWriter);
                break;

            case JoseProtectionTypes.Jws:
                VerifyJsonJws(jsonJwt.Root, secretKey, payloadWriter);
                break;

            default:
                throw new JoseException(
                    $"The protection type '{jsonJwt.ProtectionType}' is not supported."
                );
        }
    }

    private void VerifyJwsJson(
        JsonJwt jsonJwt,
        SecretKey secretKey,
        ReadOnlySpan<byte> detachedPayload
    )
    {
        if (jsonJwt.ProtectionType != JoseProtectionTypes.Jws)
            throw new InvalidOperationException(
                "Only JWS tokens can be validated with a detached payload."
            );

        List<Exception>? failures = null;
        foreach (var signature in GetJwsSignatures(jsonJwt.Root))
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

            if (!mergedHeader.TryGetPropertyValue<bool>(JoseClaimNames.Header.B64, out var b64))
                b64 = true;

            using var payloadLease = EncodeJose(b64, detachedPayload, out var encodedPayload);

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

            return;
        }

        throw new JoseIntegrityException(
            "None of the JWS signatures could be verified with the provided key.",
            AsInnerException(failures)
        );
    }

    private static List<JsonElement> GetJwsSignatures(JsonElement root)
    {
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

        return signatures;
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
        if (!element.TryGetPropertyValue<string>(name, out var value))
            throw new JoseException(
                $"The JOSE JSON serialization is missing the required '{name}' member."
            );

        return value;
    }

    private void VerifyJsonJws(
        JsonElement root,
        SecretKey secretKey,
        IBufferWriter<byte> payloadWriter
    )
    {
        if (!root.TryGetPropertyValue<string>(JoseClaimNames.Json.Payload, out var encodedPayload))
            throw new JoseException(
                "The JWS JSON serialization is missing the 'payload' member; a detached payload must be "
                    + "verified via VerifyJws."
            );

        List<Exception>? failures = null;
        foreach (var signature in GetJwsSignatures(root))
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
        IBufferWriter<byte> payloadWriter
    )
    {
        var encodedProtected = root.TryGetPropertyValue<string>(
            JoseClaimNames.Json.Protected,
            out var protectedValue
        )
            ? protectedValue
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

        var encodedAad = root.TryGetPropertyValue<string>(JoseClaimNames.Json.Aad, out var aad)
            ? aad
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

            var encodedEncryptedKey = recipient.TryGetPropertyValue<string>(
                JoseClaimNames.Json.EncryptedKey,
                out var encryptedKey
            )
                ? encryptedKey
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

        UnwrapContentKeyOrSubstitute(
            keyManagementAlgorithm,
            secretKey,
            header,
            encryptedKeyBytes,
            contentKey
        );

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

        var decryptBytesWritten = DecryptContentOrThrow(
            authenticatedEncryptionAlgorithm,
            contentKey,
            ivBytes,
            cipherTextBytes,
            associatedDataBytes,
            tagBytes,
            plainTextBytes
        );

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
            return Encode(SecureEncoding.ASCII, encodedProtected, out associatedData);

        var combined = string.Concat(encodedProtected, ".", encodedAad);
        return Encode(SecureEncoding.ASCII, combined.AsSpan(), out associatedData);
    }
}
