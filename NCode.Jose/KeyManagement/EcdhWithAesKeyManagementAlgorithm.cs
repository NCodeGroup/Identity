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
using System.Text.Json;
using NCode.Cryptography.Keys;

namespace NCode.Jose.KeyManagement;

/// <summary>
/// Provides an implementation of <see cref="KeyManagementAlgorithm"/> that uses the <c>ECDH-ES with AES</c> cryptographic algorithm for key management.
/// </summary>
public class EcdhWithAesKeyManagementAlgorithm : EcdhKeyManagementAlgorithm
{
    private IAesKeyWrap AesKeyWrap { get; }

    private int KekSizeBytes { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EcdhWithAesKeyManagementAlgorithm"/> class.
    /// </summary>
    /// <param name="aesKeyWrap">Provides the AES key wrap functionality.</param>
    /// <param name="code">Contains a <see cref="string"/> value that uniquely identifies the cryptographic algorithm.</param>
    /// <param name="kekSizeBits">Contains the size, in bits, of the derived key encryption key (KEK).</param>
    public EcdhWithAesKeyManagementAlgorithm(IAesKeyWrap aesKeyWrap, string code, int kekSizeBits)
        : base(code, isDirectAgreement: false)
    {
        AesKeyWrap = aesKeyWrap;
        KekSizeBytes = (kekSizeBits + 7) >> 3;
    }

    /// <inheritdoc />
    public override int GetEncryptedContentKeySizeBytes(int kekSizeBits, int cekSizeBytes) =>
        AesKeyWrap.GetEncryptedContentKeySizeBytes(cekSizeBytes);

    /// <inheritdoc />
    public override bool TryWrapKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        ReadOnlySpan<byte> contentKey,
        Span<byte> encryptedContentKey,
        out int bytesWritten)
    {
        if (encryptedContentKey.Length < AesKeyWrap.GetEncryptedContentKeySizeBytes(contentKey.Length))
        {
            bytesWritten = 0;
            return false;
        }

        var newKek = KekSizeBytes <= JoseConstants.MaxStackAlloc ?
            stackalloc byte[KekSizeBytes] :
            GC.AllocateUninitializedArray<byte>(KekSizeBytes, pinned: true);

        try
        {
            base.NewKey(secretKey, header, newKek);

            return AesKeyWrap.TryWrapKey(newKek, contentKey, encryptedContentKey, out bytesWritten);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(newKek);
        }
    }

    /// <inheritdoc />
    public override bool TryWrapNewKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        Span<byte> contentKey,
        Span<byte> encryptedContentKey,
        out int bytesWritten)
    {
        NewKey(secretKey, header, contentKey);
        return TryWrapKey(
            secretKey,
            header,
            contentKey,
            encryptedContentKey,
            out bytesWritten);
    }

    /// <inheritdoc />
    public override bool TryUnwrapKey(
        SecretKey secretKey,
        JsonElement header,
        ReadOnlySpan<byte> encryptedContentKey,
        Span<byte> contentKey,
        out int bytesWritten)
    {
        if (contentKey.Length < AesKeyWrap.GetContentKeySizeBytes(encryptedContentKey.Length))
        {
            bytesWritten = 0;
            return false;
        }

        var newKek = KekSizeBytes <= JoseConstants.MaxStackAlloc ?
            stackalloc byte[KekSizeBytes] :
            GC.AllocateUninitializedArray<byte>(KekSizeBytes, pinned: true);

        try
        {
            var result = base.TryUnwrapKey(secretKey, header, Array.Empty<byte>(), newKek, out var newKekBytesWritten);
            Debug.Assert(result && newKekBytesWritten == KekSizeBytes);

            return AesKeyWrap.TryUnwrapKey(newKek, encryptedContentKey, contentKey, out bytesWritten);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(newKek);
        }
    }
}
