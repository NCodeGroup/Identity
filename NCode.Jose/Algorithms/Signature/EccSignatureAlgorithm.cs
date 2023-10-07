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
using NCode.Jose.Extensions;
using NCode.Jose.SecretKeys;

namespace NCode.Jose.Algorithms.Signature;

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
    public override HashAlgorithmName HashAlgorithmName { get; }

    private int SignatureSizeBytes { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EccSignatureAlgorithm"/> class.
    /// </summary>
    /// <param name="code">Contains a <see cref="string"/> value that uniquely identifies the cryptographic algorithm.</param>
    /// <param name="hashAlgorithmName">Contains a <see cref="HashAlgorithmName"/> value that specifies the type of hash function that is used by this digital signature algorithm.</param>
    public EccSignatureAlgorithm(string code, HashAlgorithmName hashAlgorithmName)
    {
        HashAlgorithmName = hashAlgorithmName;

        var hashSizeBits = hashAlgorithmName.GetHashSizeBits();
        var kekSizeBits = hashSizeBits == 512 ? 521 : hashSizeBits;

        // ECDSA signatures are twice the size of the key size rounded up to the nearest byte
        var (quotient, remainder) = Math.DivRem(kekSizeBits * 2, 8);
        SignatureSizeBytes = quotient + remainder;

        Code = code;

        KeyBitSizes = new[] { new KeySizes(minSize: kekSizeBits, maxSize: kekSizeBits, skipSize: 0) };
    }

    /// <inheritdoc />
    public override int GetSignatureSizeBytes(int keySizeBits) => SignatureSizeBytes;

    /// <inheritdoc />
    public override bool TrySign(SecretKey secretKey, ReadOnlySpan<byte> inputData, Span<byte> signature, out int bytesWritten)
    {
        var validatedSecurityKey = secretKey.Validate<EccSecretKey>(KeyBitSizes);

        using var key = validatedSecurityKey.ExportECDsa();

        return key.TrySignData(inputData, signature, HashAlgorithmName, out bytesWritten);
    }

    /// <inheritdoc />
    public override bool Verify(SecretKey secretKey, ReadOnlySpan<byte> inputData, ReadOnlySpan<byte> signature)
    {
        var validatedSecurityKey = secretKey.Validate<EccSecretKey>(KeyBitSizes);

        using var key = validatedSecurityKey.ExportECDsa();

        return key.VerifyData(inputData, signature, HashAlgorithmName);
    }
}
