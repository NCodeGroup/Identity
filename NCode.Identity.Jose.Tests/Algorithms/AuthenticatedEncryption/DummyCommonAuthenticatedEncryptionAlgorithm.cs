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

using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Algorithms.AuthenticatedEncryption;

namespace NCode.Jose.Tests.Algorithms.AuthenticatedEncryption;

public class DummyCommonAuthenticatedEncryptionAlgorithm : CommonAuthenticatedEncryptionAlgorithm
{
    private AuthenticatedEncryptionAlgorithm Inner { get; }

    public DummyCommonAuthenticatedEncryptionAlgorithm(AuthenticatedEncryptionAlgorithm inner)
    {
        Inner = inner;
    }

    public override string Code =>
        Inner.Code;

    public override int ContentKeySizeBytes =>
        Inner.ContentKeySizeBytes;

    public override int NonceSizeBytes =>
        Inner.NonceSizeBytes;

    public override int AuthenticationTagSizeBytes =>
        Inner.AuthenticationTagSizeBytes;

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
        Span<byte> authenticationTag) =>
        Inner.Encrypt(
            cek,
            nonce,
            plainText,
            associatedData,
            cipherText,
            authenticationTag);

    public override bool TryDecrypt(
        ReadOnlySpan<byte> cek,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> cipherText,
        ReadOnlySpan<byte> associatedData,
        ReadOnlySpan<byte> authenticationTag,
        Span<byte> plainText,
        out int bytesWritten) =>
        Inner.TryDecrypt(
            cek,
            nonce,
            cipherText,
            associatedData,
            authenticationTag,
            plainText,
            out bytesWritten);
}
