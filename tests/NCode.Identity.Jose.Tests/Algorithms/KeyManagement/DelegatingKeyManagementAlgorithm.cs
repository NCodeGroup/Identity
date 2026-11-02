// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.Tests.Algorithms.KeyManagement;

internal sealed class DelegatingKeyManagementAlgorithm(KeyManagementAlgorithm inner)
    : KeyManagementAlgorithm
{
    private KeyManagementAlgorithm Inner { get; } = inner;

    public override string Code => Inner.Code;

    public override IReadOnlyList<AlgorithmKeyType> KeyTypes => Inner.KeyTypes;

    public override IEnumerable<KeySizes> GetLegalCekByteSizes(int kekSizeBits) =>
        Inner.GetLegalCekByteSizes(kekSizeBits);

    public override int GetEncryptedContentKeySizeBytes(int kekSizeBits, int cekSizeBytes) =>
        Inner.GetEncryptedContentKeySizeBytes(kekSizeBits, cekSizeBytes);

    public override void NewKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        Span<byte> contentKey
    ) => Inner.NewKey(secretKey, header, contentKey);

    public override void WrapKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        ReadOnlySpan<byte> contentKey,
        IBufferWriter<byte> encryptedContentKeyWriter
    ) => Inner.WrapKey(secretKey, header, contentKey, encryptedContentKeyWriter);

    public override void WrapNewKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        Span<byte> contentKey,
        IBufferWriter<byte> encryptedContentKeyWriter
    ) => Inner.WrapNewKey(secretKey, header, contentKey, encryptedContentKeyWriter);

    public override bool TryUnwrapKey(
        SecretKey secretKey,
        JsonElement header,
        ReadOnlySpan<byte> encryptedContentKey,
        Span<byte> contentKey,
        out int bytesWritten
    ) => Inner.TryUnwrapKey(secretKey, header, encryptedContentKey, contentKey, out bytesWritten);
}
