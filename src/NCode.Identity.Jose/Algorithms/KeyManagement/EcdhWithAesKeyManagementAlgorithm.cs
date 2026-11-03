// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using NCode.Buffers;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;

namespace NCode.Identity.Jose.Algorithms.KeyManagement;

/// <summary>
/// Provides an implementation of <see cref="KeyManagementAlgorithm"/> that uses the <c>ECDH-ES with AES</c> cryptographic algorithm for key management.
/// </summary>
/// <param name="secretKeyTypeCatalog">The registered key types, whose descriptors write and read the <c>epk</c>.</param>
/// <param name="keyAgreements">The registered key agreements; a key is agreed by the one agreement that accepts it.</param>
/// <param name="aesKeyWrap">Provides the AES key wrap functionality.</param>
/// <param name="code">Contains a <see cref="string"/> value that uniquely identifies the cryptographic algorithm.</param>
/// <param name="kekSizeBits">Contains the size, in bits, of the derived key encryption key (KEK).</param>
internal class EcdhWithAesKeyManagementAlgorithm(
    ISecretKeyTypeCatalog secretKeyTypeCatalog,
    IEnumerable<IEcdhKeyAgreement> keyAgreements,
    IAesKeyWrap aesKeyWrap,
    string code,
    int kekSizeBits
) : EcdhKeyManagementAlgorithm(secretKeyTypeCatalog, keyAgreements, code, isDirectAgreement: false)
{
    private IAesKeyWrap AesKeyWrap { get; } = aesKeyWrap;

    private int KekSizeBytes { get; } = (kekSizeBits + 7) >> 3;

    /// <inheritdoc />
    public override IEnumerable<KeySizes> GetLegalCekByteSizes(int kekSizeBits) =>
        AesKeyWrap.LegalCekByteSizes;

    /// <inheritdoc />
    public override int GetEncryptedContentKeySizeBytes(int kekSizeBits, int cekSizeBytes) =>
        AesKeyWrap.GetEncryptedContentKeySizeBytes(cekSizeBytes);

    /// <summary>
    /// Generates a random content encryption key (CEK), which <see cref="WrapKey"/> wraps with the agreed key
    /// encryption key (RFC 7518 §4.6): in key agreement with key wrapping mode the CEK is not the agreed key.
    /// </summary>
    /// <param name="secretKey">The recipient's key.</param>
    /// <param name="header">The JOSE header for the current cryptographic operation.</param>
    /// <param name="contentKey">Destination for the new content encryption key (CEK).</param>
    public override void NewKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        Span<byte> contentKey
    )
    {
        ValidateContentKeySize(secretKey.KeySizeBits, contentKey.Length);
        RandomNumberGenerator.Fill(contentKey);
    }

    /// <inheritdoc />
    public override void WrapKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        ReadOnlySpan<byte> contentKey,
        IBufferWriter<byte> encryptedContentKeyWriter
    )
    {
        using var newKekLease = BufferFactory.Rent(
            KekSizeBytes,
            isSensitive: true,
            out Span<byte> newKek
        );

        base.NewKey(secretKey, header, newKek);

        AesKeyWrap.WrapKey(newKek, contentKey, ref encryptedContentKeyWriter);
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

        WrapKey(secretKey, header, contentKey, encryptedContentKeyWriter);
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
