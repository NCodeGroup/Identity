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

using System.Security.Cryptography;
using NCode.Cryptography.Keys;

namespace NCode.Jose.KeyManagement;

/// <summary>
/// Provides an implementation of <see cref="KeyManagementAlgorithm"/> that uses the <c>ECDH-ES with AES</c> cryptographic algorithm for key management.
/// </summary>
public class EcdhWithAesKeyManagementAlgorithm : EcdhKeyManagementAlgorithm
{
    private IAesKeyWrap AesKeyWrap { get; }
    private int CekSizeBytes { get; }
    private int EncryptedCekSizeBytes { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EcdhWithAesKeyManagementAlgorithm"/> class.
    /// </summary>
    /// <param name="aesKeyWrap">Provides the AES key wrap functionality.</param>
    /// <param name="code">Contains a <see cref="string"/> value that uniquely identifies the cryptographic algorithm.</param>
    /// <param name="cekSizeBits">Contains the legal size, in bits, of the content encryption key (CEK).</param>
    public EcdhWithAesKeyManagementAlgorithm(IAesKeyWrap aesKeyWrap, string code, int cekSizeBits)
        : base(code)
    {
        AesKeyWrap = aesKeyWrap;
        CekSizeBytes = cekSizeBits >> 3;
        EncryptedCekSizeBytes = aesKeyWrap.GetEncryptedContentKeySizeBytes(CekSizeBytes);
    }

    private void ValidateContentKeySize(ReadOnlySpan<byte> contentKey)
    {
        if (contentKey.Length != CekSizeBytes)
        {
            throw new ArgumentException(
                "The content encryption key (CEK) does not have a valid size for this cryptographic algorithm.",
                nameof(contentKey));
        }
    }

    private void ValidateKeySizes(ReadOnlySpan<byte> contentKey, ReadOnlySpan<byte> encryptedContentKey)
    {
        ValidateContentKeySize(contentKey);

        if (encryptedContentKey.Length != EncryptedCekSizeBytes)
        {
            throw new ArgumentException(
                "The encrypted content encryption key (CEK) does not have a valid size for this cryptographic algorithm.",
                nameof(encryptedContentKey));
        }
    }

    /// <inheritdoc />
    public override int GetEncryptedContentKeySizeBytes(int contentKeySizeBytes) =>
        AesKeyWrap.GetEncryptedContentKeySizeBytes(contentKeySizeBytes);

    /// <inheritdoc />
    public override void NewKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        Span<byte> contentKey)
    {
        ValidateContentKeySize(contentKey);

        RandomNumberGenerator.Fill(contentKey);
    }

    /// <inheritdoc />
    public override void WrapKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        ReadOnlySpan<byte> contentKey,
        Span<byte> encryptedContentKey)
    {
        ValidateKeySizes(contentKey, encryptedContentKey);

        var keyLength = contentKey.Length;
        var newKek = keyLength <= JoseConstants.MaxStackAlloc ?
            stackalloc byte[keyLength] :
            GC.AllocateUninitializedArray<byte>(keyLength, pinned: true);

        try
        {
            base.NewKey(secretKey, header, newKek);

            AesKeyWrap.WrapKey(newKek, contentKey, encryptedContentKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(newKek);
        }
    }

    /// <inheritdoc />
    public override void UnwrapKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        ReadOnlySpan<byte> encryptedContentKey,
        Span<byte> contentKey)
    {
        ValidateKeySizes(contentKey, encryptedContentKey);

        var keyLength = contentKey.Length;
        var newKek = keyLength <= JoseConstants.MaxStackAlloc ?
            stackalloc byte[keyLength] :
            GC.AllocateUninitializedArray<byte>(keyLength, pinned: true);

        try
        {
            base.UnwrapKey(secretKey, header, Array.Empty<byte>(), newKek);

            AesKeyWrap.UnwrapKey(newKek, encryptedContentKey, contentKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(newKek);
        }
    }
}
