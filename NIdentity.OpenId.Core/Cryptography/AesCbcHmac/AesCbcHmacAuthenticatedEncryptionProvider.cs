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
using NIdentity.OpenId.Cryptography.Aead;
using NIdentity.OpenId.Cryptography.Binary;

namespace NIdentity.OpenId.Cryptography.AesCbcHmac;

/// <summary>
/// Provides an implementation of <see cref="AuthenticatedEncryptionProvider"/> using the <c>AES CBC HMAC SHA2</c> algorithm.
/// </summary>
public class AesCbcHmacAuthenticatedEncryptionProvider : AuthenticatedEncryptionProvider
{
    private const int ExpectedNonceBitLength = 128;

    private SharedSecretKey SharedSecretKey { get; }

    private AesCbcHmacAuthenticatedEncryptionAlgorithmDescriptor Descriptor { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AesCbcHmacAuthenticatedEncryptionProvider"/> class.
    /// </summary>
    /// <param name="secretKey">Contains key material for the <c>AES CBC HMAC SHA2</c> algorithm.</param>
    /// <param name="descriptor">Contains an <see cref="AesCbcHmacAuthenticatedEncryptionAlgorithmDescriptor"/> that describes the <c>AES CBC HMAC SHA2</c> algorithm.</param>
    public AesCbcHmacAuthenticatedEncryptionProvider(SharedSecretKey secretKey, AesCbcHmacAuthenticatedEncryptionAlgorithmDescriptor descriptor)
        : base(secretKey, descriptor)
    {
        SharedSecretKey = secretKey;
        Descriptor = descriptor;
    }

    private void ComputeAuthenticationTag(
        int keyByteLength,
        ReadOnlySpan<byte> hmacKey,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> associatedData,
        ReadOnlySpan<byte> cipherText,
        Span<byte> authenticationTag)
    {
        // HMAC( aad | iv | cipherText | addLength )
        // addLength = aad bit length as Int64 into bytes

        var hmacInputByteLength = associatedData.Length + nonce.Length + cipherText.Length + sizeof(long);
        var hmacInput = hmacInputByteLength <= BinaryUtility.StackAllocMax ?
            stackalloc byte[hmacInputByteLength] :
            GC.AllocateUninitializedArray<byte>(hmacInputByteLength, pinned: true);

        try
        {
            BinaryUtility.Concat(
                associatedData,
                nonce,
                cipherText,
                associatedData.Length * BinaryUtility.BitsPerByte,
                hmacInput);

            var hmacOutputByteLength = Descriptor.HashByteLength;
            var hmacOutput = hmacOutputByteLength <= BinaryUtility.StackAllocMax ?
                stackalloc byte[hmacOutputByteLength] :
                GC.AllocateUninitializedArray<byte>(hmacOutputByteLength, pinned: true);

            try
            {
                var hmacResult = Descriptor.KeyedHashFunction(hmacKey, hmacInput, hmacOutput, out var hmacBytesWritten);
                if (!hmacResult || hmacBytesWritten != hmacOutputByteLength)
                    throw new InvalidOperationException();

                hmacOutput[..keyByteLength].CopyTo(authenticationTag);
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

    /// <inheritdoc />
    public override void Encrypt(
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> plainText,
        ReadOnlySpan<byte> associatedData,
        Span<byte> cipherText,
        Span<byte> authenticationTag)
    {
        var nonceByteLength = nonce.Length;
        var nonceBitLength = nonceByteLength * BinaryUtility.BitsPerByte;
        if (nonceBitLength != ExpectedNonceBitLength)
            throw new InvalidOperationException();

        var keyByteLength = AlgorithmDescriptor.KeyByteLength;
        var keyByteLengthPair = SharedSecretKey.KeyByteLength;
        if (keyByteLength * 2 != keyByteLengthPair)
            throw new InvalidOperationException();

        if (authenticationTag.Length < keyByteLength)
            throw new InvalidOperationException();

        var hmacKey = SharedSecretKey.KeyBytes[..keyByteLength];
        var aesKey = SharedSecretKey.KeyBytes[keyByteLength..];

        // TODO: pin and zero memory for key since aes clones the property value
        using var aes = System.Security.Cryptography.Aes.Create();
        aes.Key = aesKey.ToArray();

        var cipherTextByteLength = aes.GetCiphertextLengthCbc(plainText.Length);
        if (cipherText.Length < cipherTextByteLength)
            throw new InvalidOperationException();

        var aesResult = aes.TryEncryptCbc(plainText, nonce, cipherText, out var aesBytesWritten);
        if (!aesResult || aesBytesWritten != cipherTextByteLength)
            throw new InvalidOperationException();

        ComputeAuthenticationTag(
            keyByteLength,
            hmacKey,
            nonce,
            associatedData,
            cipherText,
            authenticationTag);
    }

    /// <inheritdoc />
    public override bool TryDecrypt(
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> cipherText,
        ReadOnlySpan<byte> associatedData,
        ReadOnlySpan<byte> authenticationTag,
        Span<byte> plainText,
        out int bytesWritten)
    {
        var nonceByteLength = nonce.Length;
        var nonceBitLength = nonceByteLength * BinaryUtility.BitsPerByte;
        if (nonceBitLength != ExpectedNonceBitLength)
            throw new InvalidOperationException();

        var keyByteLength = AlgorithmDescriptor.KeyByteLength;
        var keyByteLengthPair = SharedSecretKey.KeyByteLength;
        if (keyByteLength * 2 != keyByteLengthPair)
            throw new InvalidOperationException();

        if (authenticationTag.Length != keyByteLength)
            throw new InvalidOperationException();

        const int blockBitLength = 128;
        const int blockByteLength = blockBitLength / BinaryUtility.BitsPerByte;
        if (plainText.Length + blockByteLength < cipherText.Length)
        {
            bytesWritten = 0;
            return false;
        }

        var hmacKey = SharedSecretKey.KeyBytes[..keyByteLength];
        var aesKey = SharedSecretKey.KeyBytes[keyByteLength..];

        var expectedAuthenticationTag = keyByteLength <= BinaryUtility.StackAllocMax ?
            stackalloc byte[keyByteLength] :
            GC.AllocateUninitializedArray<byte>(keyByteLength, pinned: true);

        try
        {
            ComputeAuthenticationTag(
                keyByteLength,
                hmacKey,
                nonce,
                associatedData,
                cipherText,
                expectedAuthenticationTag);

            if (!CryptographicOperations.FixedTimeEquals(expectedAuthenticationTag, authenticationTag))
                // TODO: better exception
                throw new InvalidOperationException();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(expectedAuthenticationTag);
        }

        // TODO: pin and zero memory for key since aes clones the property value
        using var aes = System.Security.Cryptography.Aes.Create();
        aes.Key = aesKey.ToArray();

        Debug.Assert(aes.BlockSize == blockBitLength);

        try
        {
            return aes.TryDecryptCbc(cipherText, nonce, plainText, out bytesWritten);
        }
        catch (CryptographicException exception)
        {
            // TODO: wrap in better exception
            throw new InvalidOperationException("Unable to decrypt ciphertext", exception);
        }
    }
}
