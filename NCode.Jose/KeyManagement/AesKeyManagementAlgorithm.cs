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
/// Provides an implementation of <see cref="KeyManagementAlgorithm"/> that uses the <c>AES</c> cryptographic algorithm for key management.
/// </summary>
public class AesKeyManagementAlgorithm : KeyManagementAlgorithm
{
    private IAesKeyWrap AesKeyWrap { get; }

    /// <inheritdoc />
    public override string Code { get; }

    /// <inheritdoc />
    public override Type SecretKeyType => typeof(SymmetricSecretKey);

    /// <inheritdoc />
    public override IEnumerable<KeySizes> KekBitSizes { get; }

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

        KekBitSizes = new[] { new KeySizes(minSize: kekSizeBits, maxSize: kekSizeBits, skipSize: 0) };
    }

    /// <inheritdoc />
    public override int GetEncryptedContentKeySizeBytes(int contentKeySizeBytes) =>
        AesKeyWrap.GetEncryptedContentKeySizeBytes(contentKeySizeBytes);

    /// <inheritdoc />
    public override void WrapKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        ReadOnlySpan<byte> contentKey,
        Span<byte> encryptedContentKey)
    {
        var validatedSecretKey = ValidateSecretKey<SymmetricSecretKey>(secretKey, KekBitSizes);

        AesKeyWrap.WrapKey(validatedSecretKey.KeyBytes, contentKey, encryptedContentKey);
    }

    /// <inheritdoc />
    public override void UnwrapKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        ReadOnlySpan<byte> encryptedContentKey,
        Span<byte> contentKey)
    {
        var validatedSecretKey = ValidateSecretKey<SymmetricSecretKey>(secretKey, KekBitSizes);

        AesKeyWrap.UnwrapKey(validatedSecretKey.KeyBytes, encryptedContentKey, contentKey);
    }
}
