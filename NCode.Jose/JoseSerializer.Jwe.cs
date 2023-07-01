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
using NCode.Buffers;
using NCode.Cryptography.Keys;
using NCode.Jose.Exceptions;
using NCode.Jose.Extensions;
using Nerdbank.Streams;
using Validation;

namespace NCode.Jose;

partial class JoseSerializer
{
    // private static bool IsJson(string value) =>
    //     value.StartsWith('{') && value.EndsWith('}');

    // private JweToken ParseJwe(string jwe, SecretKeySelectorDelegate secretKeySelector)
    // {
    //     var trimmed = jwe.Trim();
    //     var isJson = IsJson(trimmed);
    //     return isJson ? ParseJweJson(trimmed, secretKeySelector) : ParseJweCompact(trimmed, secretKeySelector);
    // }

    private delegate bool SecretKeySelectorDelegate(
        IReadOnlyDictionary<string, object> header,
        [MaybeNullWhen(false)] out SecretKey secretKey);

    private static SecretKeySelectorDelegate SecretKeySelectorFactory(IEnumerable<SecretKey> secretKeys)
    {
        var customLookup = new Dictionary<(string HeaderKey, string HeaderValue), SecretKey>();
        foreach (var secretKey in secretKeys)
        {
            customLookup[("kid", secretKey.KeyId)] = secretKey;

            if (secretKey is not AsymmetricSecretKey { Certificate: not null } asymmetricSecretKey) continue;

            var certificate = asymmetricSecretKey.Certificate;
            var thumbprintSha1 = Base64Url.Encode(certificate.GetCertHash());
            var thumbprintSha256 = Base64Url.Encode(certificate.GetCertHash(HashAlgorithmName.SHA256));

            customLookup[("x5t", thumbprintSha1)] = secretKey;
            customLookup[("x5t#S256", thumbprintSha256)] = secretKey;
        }

        var headerKeys = new[] { "kid", "x5t", "x5t#S256" };

        bool SecretKeySelector(IReadOnlyDictionary<string, object> header, [MaybeNullWhen(false)] out SecretKey secretKey)
        {
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
        ReadOnlySequenceSegment<char> iterator,
        ISecretKeyCollection secretKeys,
        out IReadOnlyDictionary<string, object> header)
    {
        var token = JweToken.ParseCompact(iterator);

        var secretKeySelector = SecretKeySelectorFactory(secretKeys);

        using var plainTextData = new Sequence<byte>(ArrayPool<byte>.Shared);

        DecryptJweCompact(ref token, secretKeySelector, plainTextData, out var localHeader);

        Debug.Assert(plainTextData.Length <= int.MaxValue);
        var plainTextLength = (int)plainTextData.Length;

        using var plainTextReader = new SequenceTextReader(plainTextData, Encoding.UTF8);

        var payload = string.Create(plainTextLength, plainTextReader, (span, reader) =>
        {
            int charsRead;
            var totalCharsRead = 0;
            do
            {
                charsRead = reader.Read(span[totalCharsRead..]);
                totalCharsRead += charsRead;
            } while (charsRead > 0);
        });

        header = localHeader;
        return payload;
    }

    private void DecryptJweCompact(
        ref JweToken token,
        SecretKeySelectorDelegate secretKeySelector,
        IBufferWriter<byte> plainTextData,
        out IReadOnlyDictionary<string, object> headers)
    {
        Requires.Argument(token.JweType == JweType.Compact, nameof(token), "The specified value is not a valid JWE token in compact form.");
        //Requires.Argument(plainTextStream.CanSeek, nameof(plainTextStream), "The specified stream must be seekable.");

        /*
              BASE64URL(UTF8(JWE Protected Header)) || '.' ||
              BASE64URL(JWE Encrypted Key) || '.' ||
              BASE64URL(JWE Initialization Vector) || '.' ||
              BASE64URL(JWE Ciphertext) || '.' ||
              BASE64URL(JWE Authentication Tag)
        */

        // JWE Protected Header
        var localHeader = DeserializeUtf8JsonAfterBase64Url<Dictionary<string, object>>(
            "JWE Protected Header",
            token.EncodedProtectedHeader);

        // JWE Encrypted Key
        using var encryptedKeyLease = DecodeBase64Url(
            "JWE Encrypted Key",
            token.EncodedEncryptedKey,
            out var encryptedKeyBytes);

        // JWE Initialization Vector
        using var initializationVectorLease = DecodeBase64Url(
            "JWE Initialization Vector",
            token.EncodedInitializationVector,
            out var initializationVectorBytes);

        // JWE Ciphertext
        using var cipherTextLease = DecodeBase64Url(
            "JWE Ciphertext",
            token.EncodedCiphertext,
            out var cipherTextBytes);

        // JWE Authentication Tag
        using var authenticationTagLease = DecodeBase64Url(
            "JWE Authentication Tag",
            token.EncodedAuthenticationTag,
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

        if (!secretKeySelector(localHeader, out var secretKey))
        {
            throw new JoseException("Unable to determine the JWE encryption key.");
        }

        var cekSizeBytes = encryptionAlgorithm.ContentKeySizeBytes;
        using var contentKeyLease = CryptoPool.Rent(cekSizeBytes);
        var contentKey = contentKeyLease.Memory.Span[..cekSizeBytes];

        var unwrapResult = keyManagementAlgorithm.TryUnwrapKey(
            secretKey,
            localHeader,
            encryptedKeyBytes,
            contentKey,
            out var unwrapBytesWritten);

        if (!unwrapResult || unwrapBytesWritten != cekSizeBytes)
        {
            throw new EncryptionJoseException("Failed to decrypt the encrypted content encryption key (CEK).");
        }

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

        var associatedDataByteCount = Encoding.ASCII.GetByteCount(token.EncodedProtectedHeader);
        using var associatedDataLease = CryptoPool.Rent(associatedDataByteCount);
        var associatedDataBytes = associatedDataLease.Memory.Span[..associatedDataByteCount];
        Encoding.ASCII.GetBytes(token.EncodedProtectedHeader, associatedDataBytes);

        var plainTextSizeBytes = encryptionAlgorithm.GetMaxPlainTextSizeBytes(cipherTextBytes.Length);
        using var plainTextLease = CryptoPool.Rent(plainTextSizeBytes);
        var plainTextBytes = plainTextLease.Memory.Span[..plainTextSizeBytes];

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

        if (!decryptResult || decryptBytesWritten != plainTextSizeBytes)
        {
            throw new EncryptionJoseException("Failed to decrypt the JWE Ciphertext.");
        }

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

            compressionAlgorithm.Decompress(plainTextBytes, plainTextData);
        }
        else
        {
            plainTextData.Write(plainTextBytes);
        }

        headers = localHeader;
    }
}
