// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Algorithms.AuthenticatedEncryption;

namespace NCode.Identity.Jose.Tests.Algorithms.AuthenticatedEncryption;

internal class DummyCommonAuthenticatedEncryptionAlgorithm : CommonAuthenticatedEncryptionAlgorithm
{
    private AuthenticatedEncryptionAlgorithm Inner { get; }

    public DummyCommonAuthenticatedEncryptionAlgorithm(AuthenticatedEncryptionAlgorithm inner)
    {
        Inner = inner;
    }

    public override string Code => Inner.Code;

    public override int ContentKeySizeBytes => Inner.ContentKeySizeBytes;

    public override int NonceSizeBytes => Inner.NonceSizeBytes;

    public override int AuthenticationTagSizeBytes => Inner.AuthenticationTagSizeBytes;

    public override int GetCipherTextSizeBytes(int plainTextSizeBytes) =>
        Inner.GetCipherTextSizeBytes(plainTextSizeBytes);

    public override int GetMaxPlainTextSizeBytes(int cipherTextSizeBytes) =>
        Inner.GetMaxPlainTextSizeBytes(cipherTextSizeBytes);

    public override void Encrypt(
        ReadOnlySpan<byte> cek,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> plainText,
        ReadOnlySpan<byte> associatedData,
        Span<byte> cipherText,
        Span<byte> authenticationTag
    ) => Inner.Encrypt(cek, nonce, plainText, associatedData, cipherText, authenticationTag);

    public override bool TryDecrypt(
        ReadOnlySpan<byte> cek,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> cipherText,
        ReadOnlySpan<byte> associatedData,
        ReadOnlySpan<byte> authenticationTag,
        Span<byte> plainText,
        out int bytesWritten
    ) =>
        Inner.TryDecrypt(
            cek,
            nonce,
            cipherText,
            associatedData,
            authenticationTag,
            plainText,
            out bytesWritten
        );
}
