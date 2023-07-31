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
using NCode.Jose.KeyManagement;

namespace NCode.Jose.Tests.KeyManagement;

public class DelegatingKeyManagementAlgorithm : KeyManagementAlgorithm
{
    private IKeyManagementAlgorithm Inner { get; }

    public DelegatingKeyManagementAlgorithm(IKeyManagementAlgorithm inner) =>
        Inner = inner;

    public override string Code => Inner.Code;

    public override Type KeyType => Inner.KeyType;

    public override IEnumerable<KeySizes> KeyBitSizes => Inner.KeyBitSizes;

    public override IEnumerable<KeySizes> GetLegalCekByteSizes(int kekSizeBits) =>
        Inner.GetLegalCekByteSizes(kekSizeBits);

    public override int GetEncryptedContentKeySizeBytes(int kekSizeBits, int cekSizeBytes) =>
        Inner.GetEncryptedContentKeySizeBytes(kekSizeBits, cekSizeBytes);

    public override bool TryWrapKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        ReadOnlySpan<byte> contentKey,
        Span<byte> encryptedContentKey,
        out int bytesWritten) =>
        Inner.TryWrapKey(
            secretKey,
            header,
            contentKey,
            encryptedContentKey,
            out bytesWritten);

    public override bool TryUnwrapKey(
        SecretKey secretKey,
        IReadOnlyDictionary<string, object> header,
        ReadOnlySpan<byte> encryptedContentKey,
        Span<byte> contentKey,
        out int bytesWritten) =>
        Inner.TryUnwrapKey(
            secretKey,
            header,
            encryptedContentKey,
            contentKey,
            out bytesWritten);
}
