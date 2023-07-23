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
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NCode.Buffers;
using NCode.Cryptography.Keys;
using NCode.CryptoMemory;
using NCode.Encoders;
using NCode.Jose.Exceptions;
using NCode.Jose.Extensions;
using Nerdbank.Streams;

namespace NCode.Jose;

partial class JoseSerializer
{
    // private static bool IsJson(string value) =>
    //     value.StartsWith('{') && value.EndsWith('}');

    private delegate bool SecretKeySelectorDelegate(
        IReadOnlyDictionary<string, object> header,
        [MaybeNullWhen(false)] out SecretKey secretKey);

    private static SecretKeySelectorDelegate SecretKeySelectorFactory(ISecretKeyCollection secretKeys)
    {
        var headerKeys = new[] { "x5t", "x5t#S256" };
        IReadOnlyDictionary<(string HeaderKey, string HeaderValue), SecretKey>? customLookup = null;

        IReadOnlyDictionary<(string HeaderKey, string HeaderValue), SecretKey> CreateCustomLookup()
        {
            var newCustomLookup = new Dictionary<(string HeaderKey, string HeaderValue), SecretKey>();

            foreach (var secretKey in secretKeys)
            {
                if (secretKey is not AsymmetricSecretKey { Certificate: not null } asymmetricSecretKey) continue;

                var certificate = asymmetricSecretKey.Certificate;
                var thumbprintSha1 = Base64Url.Encode(certificate.GetCertHash());
                var thumbprintSha256 = Base64Url.Encode(certificate.GetCertHash(HashAlgorithmName.SHA256));

                newCustomLookup[("x5t", thumbprintSha1)] = secretKey;
                newCustomLookup[("x5t#S256", thumbprintSha256)] = secretKey;
            }

            return newCustomLookup;
        }

        bool SecretKeySelector(IReadOnlyDictionary<string, object> header, [MaybeNullWhen(false)] out SecretKey secretKey)
        {
            if (header.TryGetValue<string>("kid", out var keyId) && secretKeys.TryGetByKeyId(keyId, out secretKey))
            {
                return true;
            }

            customLookup ??= CreateCustomLookup();

            foreach (var headerKey in headerKeys)
            {
                if (header.TryGetValue<string>(headerKey, out var headerValue) &&
                    customLookup.TryGetValue((headerKey, headerValue), out secretKey))
                {
                    return true;
                }
            }

            secretKey = null;
            return false;
        }

        return SecretKeySelector;
    }

    private string DecodeJweCompact(
        StringSegments segments,
        ISecretKeyCollection secretKeys,
        out IReadOnlyDictionary<string, object> header)
    {
        using var byteSequence = new Sequence<byte>(ArrayPool<byte>.Shared);

        DecryptJweCompact(segments, secretKeys, byteSequence, out var localHeader);

        var payload = DecodeUtf8(byteSequence);

        header = localHeader;
        return payload;
    }

    private T? DeserializeJweCompact<T>(
        StringSegments segments,
        ISecretKeyCollection secretKeys,
        JsonSerializerOptions options,
        out IReadOnlyDictionary<string, object> header)
    {
        using var byteSequence = new Sequence<byte>(ArrayPool<byte>.Shared);

        DecryptJweCompact(segments, secretKeys, byteSequence, out var localHeader);

        var payload = Deserialize<T>(byteSequence, options);

        header = localHeader;
        return payload;
    }

    private void DecryptJweCompact(
        StringSegments segments,
        ISecretKeyCollection secretKeys,
        IBufferWriter<byte> payloadBytes,
        out IReadOnlyDictionary<string, object> header)
    {
        /*
              BASE64URL(UTF8(JWE Protected Header)) || '.' ||
              BASE64URL(JWE Encrypted Key) || '.' ||
              BASE64URL(JWE Initialization Vector) || '.' ||
              BASE64URL(JWE Ciphertext) || '.' ||
              BASE64URL(JWE Authentication Tag)
        */
        Debug.Assert(segments.Count == JweSegmentCount);

        // JWE Protected Header
        var jweProtectedHeader = segments.First;
        var encodedHeader = jweProtectedHeader.Memory.Span;
        var localHeader = DeserializeHeader(
            "JWE Protected Header",
            encodedHeader);

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

        if (!localHeader.TryGetValue<string>("alg", out var keyManagementAlgorithmCode))
        {
            throw new JoseException("The JWE header is missing the 'alg' field.");
        }

        if (!AlgorithmProvider.TryGetKeyManagementAlgorithm(keyManagementAlgorithmCode, out var keyManagementAlgorithm))
        {
            throw new InvalidAlgorithmJoseException($"No registered JWA key agreement algorithm for `{keyManagementAlgorithmCode}` was found.");
        }

        if (!localHeader.TryGetValue<string>("enc", out var encryptionAlgorithmCode))
        {
            throw new JoseException("The JWE header is missing the 'enc' field.");
        }

        if (!AlgorithmProvider.TryGetAuthenticatedEncryptionAlgorithm(encryptionAlgorithmCode, out var encryptionAlgorithm))
        {
            throw new InvalidAlgorithmJoseException($"No registered AEAD encryption algorithm for `{encryptionAlgorithmCode}` was found.");
        }

        var secretKeySelector = SecretKeySelectorFactory(secretKeys);
        if (!secretKeySelector(localHeader, out var secretKey))
        {
            throw new JoseException("Unable to determine the JWE encryption key.");
        }

        var cekSizeBytes = encryptionAlgorithm.ContentKeySizeBytes;
        using var contentKeyLease = CryptoPool.Rent(cekSizeBytes, isSensitive: true, out Span<byte> contentKey);

        var unwrapResult = keyManagementAlgorithm.TryUnwrapKey(
            secretKey,
            localHeader,
            encryptedKeyBytes,
            contentKey,
            out var unwrapBytesWritten);

        if (!unwrapResult || unwrapBytesWritten == 0)
        {
            throw new EncryptionJoseException("Failed to decrypt the encrypted content encryption key (CEK).");
        }

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

        var associatedDataByteCount = Encoding.ASCII.GetByteCount(encodedHeader);
        using var associatedDataLease = CryptoPool.Rent(associatedDataByteCount, isSensitive: false, out Span<byte> associatedDataBytes);
        var addBytesWritten = Encoding.ASCII.GetBytes(encodedHeader, associatedDataBytes);
        Debug.Assert(addBytesWritten == associatedDataByteCount);

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
        {
            throw new EncryptionJoseException("Failed to decrypt the JWE Ciphertext.");
        }

        if (decryptBytesWritten < plainTextSizeBytes)
            plainTextBytes = plainTextBytes[..decryptBytesWritten];

        /*
           17.  If a "zip" parameter was included, uncompress the decrypted
                plaintext using the specified compression algorithm.
        */

        if (localHeader.TryGetValue<string>("zip", out var compressionAlgorithmCode))
        {
            if (!AlgorithmProvider.TryGetCompressionAlgorithm(compressionAlgorithmCode, out var compressionAlgorithm))
            {
                throw new InvalidAlgorithmJoseException($"No registered JWE compression algorithm for `{compressionAlgorithmCode}` was found.");
            }

            compressionAlgorithm.Decompress(plainTextBytes, payloadBytes);
        }
        else
        {
            payloadBytes.Write(plainTextBytes);
        }

        header = localHeader;
    }
}
