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
using System.Text.Json;
using JetBrains.Annotations;
using NCode.Buffers;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.Algorithms.KeyManagement;

/// <summary>
/// Provides an implementation of <see cref="KeyManagementAlgorithm"/> that uses the <c>ECDH-ES with AES</c> cryptographic algorithm for key management.
/// </summary>
[PublicAPI]
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
    public override void WrapKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        ReadOnlySpan<byte> contentKey,
        IBufferWriter<byte> encryptedContentKeyWriter
    )
    {
        var newKek = KekSizeBytes <= JoseConstants.MaxStackAlloc ?
            stackalloc byte[KekSizeBytes] :
            GC.AllocateUninitializedArray<byte>(KekSizeBytes, pinned: true);

        try
        {
            base.NewKey(secretKey, header, newKek);

            AesKeyWrap.WrapKey(newKek, contentKey, ref encryptedContentKeyWriter);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(newKek);
        }
    }

    /// <inheritdoc />
    public override void WrapNewKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        Span<byte> contentKey,
        IBufferWriter<byte> encryptedContentKeyWriter
    )
    {
        NewKey(secretKey, header, contentKey);

        WrapKey(
            secretKey,
            header,
            contentKey,
            encryptedContentKeyWriter
        );
    }

    /// <inheritdoc />
    public override bool TryUnwrapKey(
        SecretKey secretKey,
        JsonElement header,
        ReadOnlySpan<byte> encryptedContentKey,
        Span<byte> contentKey,
        out int bytesWritten
    )
    {
        if (contentKey.Length < AesKeyWrap.GetContentKeySizeBytes(encryptedContentKey.Length))
        {
            bytesWritten = 0;
            return false;
        }

        using var newKek = BufferFactory.CreatePinnedArray(KekSizeBytes);

        var result = base.TryUnwrapKey(secretKey, header, [], newKek, out var newKekBytesWritten);
        Debug.Assert(result && newKekBytesWritten == KekSizeBytes);

        var contentKeyWriter = contentKey.GetFixedBufferWriter();

        AesKeyWrap.UnwrapKey(newKek, encryptedContentKey, ref contentKeyWriter);

        bytesWritten = contentKeyWriter.WrittenCount;
        return true;
    }
}
