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

namespace NCode.Jose.Signature;

/// <summary>
/// Provides an implementation of <see cref="SignatureAlgorithm"/> that uses an <c>Elliptic-Curve (ECC)</c> cryptographic algorithm for digital signatures.
/// </summary>
public class EccSignatureAlgorithm : SignatureAlgorithm
{
    /// <inheritdoc />
    public override string Code { get; }

    /// <inheritdoc />
    public override Type KeyType => typeof(EccSecretKey);

    /// <inheritdoc />
    public override IEnumerable<KeySizes> KeyBitSizes { get; }

    /// <inheritdoc />
    public override int SignatureSizeBits { get; }

    private HashAlgorithmName HashAlgorithmName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EccSignatureAlgorithm"/> class.
    /// </summary>
    /// <param name="code">Contains a <see cref="string"/> value that uniquely identifies the cryptographic algorithm.</param>
    /// <param name="hashAlgorithmName">Contains a <see cref="HashAlgorithmName"/> value that specifies the type of hash function to use.</param>
    public EccSignatureAlgorithm(string code, HashAlgorithmName hashAlgorithmName)
    {
        var hashSize = HashSizeBitsFromAlgorithmName(hashAlgorithmName);

        Code = code;
        SignatureSizeBits = hashSize;
        HashAlgorithmName = hashAlgorithmName;

        var kekSizeBits = hashSize == 512 ? 521 : hashSize;
        KeyBitSizes = new[] { new KeySizes(minSize: kekSizeBits, maxSize: kekSizeBits, skipSize: 0) };
    }

    /// <inheritdoc />
    public override bool TrySign(SecretKey secretKey, ReadOnlySpan<byte> inputData, Span<byte> signature, out int bytesWritten)
    {
        var validatedSecurityKey = ValidateSecretKey<EccSecretKey>(secretKey);

        using var key = validatedSecurityKey.ExportECDsa();

        return key.TrySignData(inputData, signature, HashAlgorithmName, out bytesWritten);
    }

    /// <inheritdoc />
    public override bool Verify(SecretKey secretKey, ReadOnlySpan<byte> inputData, ReadOnlySpan<byte> signature)
    {
        var validatedSecurityKey = ValidateSecretKey<EccSecretKey>(secretKey);

        using var key = validatedSecurityKey.ExportECDsa();

        return key.VerifyData(inputData, signature, HashAlgorithmName);
    }
}
