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
using JetBrains.Annotations;
using NCode.Identity.Secrets;

namespace NCode.Identity.Jose.Algorithms.Signature;

/// <summary>
/// Provides an implementation of <see cref="SignatureAlgorithm"/> that uses an <c>RSA</c> cryptographic algorithm for digital signatures.
/// </summary>
[PublicAPI]
public class RsaSignatureAlgorithm : SignatureAlgorithm
{
    /*
        Digital Signature with RSASSA-PKCS1-v1_5
        Digital Signature with RSASSA-PSS
        A key of size 2048 bits or larger MUST be used with these algorithms.
    */
    private static IEnumerable<KeySizes> StaticKeyBitSizes { get; } = new[]
    {
        new KeySizes(minSize: 2048, maxSize: 16384, skipSize: 64)
    };

    /// <inheritdoc />
    public override string Code { get; }

    /// <inheritdoc />
    public override Type KeyType => typeof(RsaSecretKey);

    /// <inheritdoc />
    public override IEnumerable<KeySizes> KeyBitSizes => StaticKeyBitSizes;

    /// <inheritdoc />
    public override HashAlgorithmName HashAlgorithmName { get; }

    private RSASignaturePadding Padding { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RsaSignatureAlgorithm"/> class.
    /// </summary>
    /// <param name="code">Contains a <see cref="string"/> value that uniquely identifies the cryptographic algorithm.</param>
    /// <param name="hashAlgorithmName">Contains a <see cref="HashAlgorithmName"/> value that specifies the type of hash function that is used by this digital signature algorithm.</param>
    /// <param name="padding">Contains a <see cref="RSASignaturePadding"/> value that specifies the type of <c>RSA</c> padding to use.</param>
    public RsaSignatureAlgorithm(string code, HashAlgorithmName hashAlgorithmName, RSASignaturePadding padding)
    {
        Code = code;
        HashAlgorithmName = hashAlgorithmName;
        Padding = padding;
    }

    /// <inheritdoc />
    public override int GetSignatureSizeBytes(int keySizeBits) => (keySizeBits + 7) >> 3;

    /// <inheritdoc />
    public override bool TrySign(SecretKey secretKey, ReadOnlySpan<byte> inputData, Span<byte> signature, out int bytesWritten)
    {
        var validatedSecurityKey = secretKey.Validate<RsaSecretKey>(KeyBitSizes);

        using var key = validatedSecurityKey.ExportRSA();

        return key.TrySignData(
            inputData,
            signature,
            HashAlgorithmName,
            Padding,
            out bytesWritten);
    }

    /// <inheritdoc />
    public override bool Verify(SecretKey secretKey, ReadOnlySpan<byte> inputData, ReadOnlySpan<byte> signature)
    {
        var validatedSecurityKey = secretKey.Validate<RsaSecretKey>(KeyBitSizes);

        using var key = validatedSecurityKey.ExportRSA();

        return key.VerifyData(
            inputData,
            signature,
            HashAlgorithmName,
            Padding);
    }
}
