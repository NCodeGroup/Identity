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
using NCode.Identity.Secrets.Logic;

namespace NCode.Identity.Jose.Algorithms.KeyManagement;

/// <summary>
/// Provides an implementation of <see cref="KeyManagementAlgorithm"/> that uses the <c>AES</c> cryptographic algorithm for key management.
/// </summary>
[PublicAPI]
public class AesKeyManagementAlgorithm : CommonKeyManagementAlgorithm
{
    private IAesKeyWrap AesKeyWrap { get; }

    /// <inheritdoc />
    public override string Code { get; }

    /// <inheritdoc />
    public override Type KeyType => typeof(SymmetricSecretKey);

    /// <inheritdoc />
    public override IEnumerable<KeySizes> KeyBitSizes { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AesKeyManagementAlgorithm"/> class.
    /// </summary>
    /// <param name="aesKeyWrap">Provides the AES key wrap functionality.</param>
    /// <param name="code">Contains a <see cref="string"/> value that uniquely identifies the cryptographic algorithm.</param>
    /// <param name="kekSizeBits">Contains the legal size, in bits, of the key encryption key (KEK).</param>
    public AesKeyManagementAlgorithm(IAesKeyWrap aesKeyWrap, string code, int kekSizeBits)
    {
        AesKeyWrap = aesKeyWrap;
        Code = code;

        KeyBitSizes = [new KeySizes(minSize: kekSizeBits, maxSize: kekSizeBits, skipSize: 0)];
    }

    /// <inheritdoc />
    public override IEnumerable<KeySizes> GetLegalCekByteSizes(int kekSizeBits) =>
        AesKeyWrap.LegalCekByteSizes;

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
        var validatedSecretKey = secretKey.Validate<SymmetricSecretKey>(KeyBitSizes);

        // increase our chances for a single-segment buffer
        using var privateKeyBuffer = BufferFactory.CreatePooledBufferWriter(isSensitive: true, secretKey.KeySizeBytes);
        IBufferWriter<byte> privateKeyWriter = privateKeyBuffer;

        validatedSecretKey.ExportPrivateKey(ref privateKeyWriter);
        Debug.Assert(privateKeyBuffer.Length == validatedSecretKey.KeySizeBytes);

        using var privateKeySpanLease = privateKeyBuffer.GetSpanLease(isSensitive: true);
        var privateKeySpan = privateKeySpanLease.Span;

        AesKeyWrap.WrapKey(
            privateKeySpan,
            contentKey,
            ref encryptedContentKeyWriter
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
        var validatedSecretKey = secretKey.Validate<SymmetricSecretKey>(KeyBitSizes);

        if (contentKey.Length < AesKeyWrap.GetContentKeySizeBytes(encryptedContentKey.Length))
        {
            bytesWritten = 0;
            return false;
        }

        // increase our chances for a single-segment buffer
        using var privateKeyBuffer = BufferFactory.CreatePooledBufferWriter(true, secretKey.KeySizeBytes);
        IBufferWriter<byte> privateKeyWriter = privateKeyBuffer;

        validatedSecretKey.ExportPrivateKey(ref privateKeyWriter);
        Debug.Assert(privateKeyBuffer.Length == validatedSecretKey.KeySizeBytes);

        using var privateKeySpanLease = privateKeyBuffer.GetSpanLease(isSensitive: true);
        var privateKeySpan = privateKeySpanLease.Span;

        var contentKeyWriter = contentKey.GetFixedBufferWriter();

        AesKeyWrap.UnwrapKey(
            privateKeySpan,
            encryptedContentKey,
            ref contentKeyWriter
        );

        bytesWritten = contentKeyWriter.WrittenCount;
        return true;
    }
}
