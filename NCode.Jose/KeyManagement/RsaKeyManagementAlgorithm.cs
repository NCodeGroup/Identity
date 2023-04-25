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

public class RsaKeyManagementAlgorithm : KeyManagementAlgorithm
{
    private static IEnumerable<KeySizes> StaticKekBitSizes { get; } = new[]
    {
        new KeySizes(minSize: 2048, maxSize: 16384, skipSize: 64)
    };

    /// <inheritdoc />
    public override string Code { get; }

    /// <inheritdoc />
    public override Type SecretKeyType => typeof(RsaSecretKey);

    /// <inheritdoc />
    public override IEnumerable<KeySizes> KekBitSizes => StaticKekBitSizes;

    private RSAEncryptionPadding Padding { get; }

    public RsaKeyManagementAlgorithm(string code, RSAEncryptionPadding padding)
    {
        Code = code;
        Padding = padding;
    }

    /// <inheritdoc />
    public override int GetEncryptedContentKeySizeBytes(int contentKeySizeBytes)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override void WrapKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        ReadOnlySpan<byte> contentKey,
        Span<byte> encryptedContentKey)
    {
        var validatedSecretKey = ValidateSecretKey<RsaSecretKey>(secretKey, KekBitSizes);

        using var key = validatedSecretKey.ExportRSA();

        var result = key.TryEncrypt(contentKey, encryptedContentKey, Padding, out var bytesWritten);

        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override void UnwrapKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        ReadOnlySpan<byte> encryptedContentKey,
        Span<byte> contentKey)
    {
        var validatedSecretKey = ValidateSecretKey<RsaSecretKey>(secretKey, KekBitSizes);

        using var key = validatedSecretKey.ExportRSA();

        throw new NotImplementedException();
    }
}
