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

using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using JetBrains.Annotations;
using NCode.Identity.Jose.Exceptions;

namespace NCode.Identity.Jose.Algorithms.AuthenticatedEncryption;

/// <summary>
/// Provides an implementation of <see cref="AuthenticatedEncryptionAlgorithm"/> that uses the <c>AES CBC HMAC</c> cryptographic algorithm for authenticated encryption (AEAD).
/// </summary>
[PublicAPI]
public class AesCbcHmacAuthenticatedEncryptionAlgorithm : CommonAuthenticatedEncryptionAlgorithm
{
    private const int BlockSizeBits = 128;
    private const int BlockSizeBytes = BlockSizeBits >> 3;

    /// <inheritdoc />
    public override string Code { get; }

    /// <inheritdoc />
    public override int ContentKeySizeBytes { get; }

    /// <inheritdoc />
    public override int NonceSizeBytes => BlockSizeBytes;

    /// <inheritdoc />
    public override int AuthenticationTagSizeBytes { get; }

    private KeyedHashFunctionDelegate KeyedHashFunction { get; }

    private int ComponentSizeBytes { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AesCbcHmacAuthenticatedEncryptionAlgorithm"/> class.
    /// </summary>
    /// <param name="code">Contains a <see cref="string"/> value that uniquely identifies the cryptographic algorithm.</param>
    /// <param name="keyedHashFunction">Contains a delegate for the <c>keyed hash (HMAC)</c> function to use.</param>
    /// <param name="cekSizeBits">Contains the legal size, in bits, of the content encryption key (CEK).</param>
    public AesCbcHmacAuthenticatedEncryptionAlgorithm(string code, KeyedHashFunctionDelegate keyedHashFunction, int cekSizeBits)
    {
        var cekSizeBytes = (cekSizeBits + 7) >> 3;

        Code = code;
        KeyedHashFunction = keyedHashFunction;
        ContentKeySizeBytes = cekSizeBytes;
        ComponentSizeBytes = cekSizeBytes >> 1; // half of the key size
        AuthenticationTagSizeBytes = ComponentSizeBytes;
    }

    /// <inheritdoc />
    public override int GetCipherTextSizeBytes(int plainTextSizeBytes) =>
        BlockSizeBytes * (plainTextSizeBytes / BlockSizeBytes) + BlockSizeBytes;

    /// <inheritdoc />
    public override int GetMaxPlainTextSizeBytes(int cipherTextSizeBytes) =>
        cipherTextSizeBytes;

    internal static Aes CreateAes(ReadOnlySpan<byte> key)
    {
        var aes = Aes.Create();
        Debug.Assert(aes.BlockSize == BlockSizeBits);

        // no point in using SecureMemory here since Aes clones the key anyway
        var bytes = GC.AllocateUninitializedArray<byte>(key.Length, pinned: true);
        try
        {
            key.CopyTo(bytes);
            aes.Key = bytes;
            return aes;
        }
        catch
        {
            aes.Dispose();
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes.AsSpan());
        }
    }

    /// <inheritdoc />
    public override void Encrypt(
        ReadOnlySpan<byte> cek,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> plainText,
        ReadOnlySpan<byte> associatedData,
        Span<byte> cipherText,
        Span<byte> authenticationTag)
    {
        ValidateParameters(
            encrypt: true,
            cek,
            nonce,
            plainText,
            cipherText,
            authenticationTag);

        var hmacKey = cek[..ComponentSizeBytes];
        var aesKey = cek[ComponentSizeBytes..];

        using var aes = CreateAes(aesKey);
        var result = aes.TryEncryptCbc(plainText, nonce, cipherText, out var bytesWritten);
        Debug.Assert(result && bytesWritten == cipherText.Length);

        ComputeAuthenticationTag(
            hmacKey,
            nonce,
            associatedData,
            cipherText,
            authenticationTag);
    }

    /// <inheritdoc />
    public override bool TryDecrypt(
        ReadOnlySpan<byte> cek,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> cipherText,
        ReadOnlySpan<byte> associatedData,
        ReadOnlySpan<byte> authenticationTag,
        Span<byte> plainText,
        out int bytesWritten)
    {
        ValidateParameters(
            encrypt: false,
            cek,
            nonce,
            plainText,
            cipherText,
            authenticationTag);

        var hmacKey = cek[..ComponentSizeBytes];
        var aesKey = cek[ComponentSizeBytes..];

        var expectedAuthenticationTag = ComponentSizeBytes <= JoseConstants.MaxStackAlloc ?
            stackalloc byte[ComponentSizeBytes] :
            GC.AllocateUninitializedArray<byte>(ComponentSizeBytes, pinned: true);

        try
        {
            ComputeAuthenticationTag(
                hmacKey,
                nonce,
                associatedData,
                cipherText,
                expectedAuthenticationTag);

            if (!CryptographicOperations.FixedTimeEquals(expectedAuthenticationTag, authenticationTag))
                throw new JoseIntegrityException("Failed to verify authentication tag.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(expectedAuthenticationTag);
        }

        using var aes = CreateAes(aesKey);
        try
        {
            return aes.TryDecryptCbc(cipherText, nonce, plainText, out bytesWritten);
        }
        catch (CryptographicException exception)
        {
            throw new JoseEncryptionException("Failed to decrypt ciphertext.", exception);
        }
    }

    private void ComputeAuthenticationTag(
        ReadOnlySpan<byte> hmacKey,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> associatedData,
        ReadOnlySpan<byte> cipherText,
        Span<byte> authenticationTag)
    {
        // HMAC( aad | iv | cipherText | addLength )
        // addLength = aad bit length as Int64 into bytes

        var hmacInputByteCount = associatedData.Length + nonce.Length + cipherText.Length + sizeof(long);
        var hmacInput = hmacInputByteCount <= JoseConstants.MaxStackAlloc ?
            stackalloc byte[hmacInputByteCount] :
            GC.AllocateUninitializedArray<byte>(hmacInputByteCount, pinned: true);

        try
        {
            Concat(
                associatedData,
                nonce,
                cipherText,
                hmacInput);

            var hmacOutput = ContentKeySizeBytes <= JoseConstants.MaxStackAlloc ?
                stackalloc byte[ContentKeySizeBytes] :
                GC.AllocateUninitializedArray<byte>(ContentKeySizeBytes, pinned: true);

            try
            {
                var hmacResult = KeyedHashFunction(hmacKey, hmacInput, hmacOutput, out var hmacBytesWritten);
                if (!hmacResult || hmacBytesWritten != ContentKeySizeBytes)
                    throw new InvalidOperationException();

                hmacOutput[..ComponentSizeBytes].CopyTo(authenticationTag);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(hmacOutput);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(hmacInput);
        }
    }

    private static void Concat(
        ReadOnlySpan<byte> associatedData,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> cipherText,
        Span<byte> destination)
    {
        // HMAC( aad | iv | cipherText | addLength )
        // addLength = aad bit length as Int64 into bytes

        if (associatedData.Length + nonce.Length + cipherText.Length + sizeof(long) != destination.Length)
            throw new InvalidOperationException();

        associatedData.CopyTo(destination);
        destination = destination[associatedData.Length..];

        nonce.CopyTo(destination);
        destination = destination[nonce.Length..];

        cipherText.CopyTo(destination);
        destination = destination[cipherText.Length..];

        BinaryPrimitives.WriteInt64BigEndian(destination, associatedData.Length << 3);
    }
}
