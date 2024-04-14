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

using System.Diagnostics;
using System.Security.Cryptography;
using NCode.Jose.Buffers;
using NCode.Jose.Extensions;
using NCode.Jose.SecretKeys;

namespace NCode.Jose.Algorithms.Signature;

/// <summary>
/// Provides an implementation of <see cref="SignatureAlgorithm"/> that uses a <c>keyed hash (HMAC)</c> cryptographic algorithm for digital signatures.
/// </summary>
public class KeyedHashSignatureAlgorithm : SignatureAlgorithm
{
    /// <inheritdoc />
    public override string Code { get; }

    /// <inheritdoc />
    public override Type KeyType => typeof(SymmetricSecretKey);

    /// <inheritdoc />
    public override IEnumerable<KeySizes> KeyBitSizes { get; }

    /// <inheritdoc />
    public override HashAlgorithmName HashAlgorithmName { get; }

    private int SignatureSizeBytes { get; }

    private KeyedHashFunctionDelegate KeyedHashFunction { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="KeyedHashSignatureAlgorithm"/> class.
    /// </summary>
    /// <param name="code">Contains a <see cref="string"/> value that uniquely identifies the cryptographic algorithm.</param>
    /// <param name="hashAlgorithmName">Contains a <see cref="HashAlgorithmName"/> value that specifies the type of hash function that is used by this digital signature algorithm.</param>
    /// <param name="keyedHashFunction">Contains a delegate for the <c>keyed hash (HMAC)</c> function to use.</param>
    public KeyedHashSignatureAlgorithm(
        string code,
        HashAlgorithmName hashAlgorithmName,
        KeyedHashFunctionDelegate keyedHashFunction)
    {
        var signatureSizeBits = hashAlgorithmName.GetHashSizeBits();

        Code = code;
        HashAlgorithmName = hashAlgorithmName;
        SignatureSizeBytes = (signatureSizeBits + 7) >> 3;
        KeyedHashFunction = keyedHashFunction;

        /*
           A key of the same size as the hash output (for instance, 256 bits for
           "HS256") or larger MUST be used with this algorithm.  (This
           requirement is based on Section 5.3.4 (Security Effect of the HMAC
           Key) of NIST SP 800-117 [NIST.800-107], which states that the
           effective security strength is the minimum of the security strength
           of the key and two times the size of the internal hash value.)
        */
        KeyBitSizes = new[] { new KeySizes(minSize: signatureSizeBits, maxSize: int.MaxValue, skipSize: 8) };
    }

    /// <inheritdoc />
    public override int GetSignatureSizeBytes(int keySizeBits) => SignatureSizeBytes;

    /// <inheritdoc />
    public override bool TrySign(SecretKey secretKey, ReadOnlySpan<byte> inputData, Span<byte> signature, out int bytesWritten)
    {
        var validatedSecretKey = secretKey.Validate<SymmetricSecretKey>(KeyBitSizes);

        using var _ = CryptoPool.Rent(
            validatedSecretKey.KeySizeBytes,
            isSensitive: true,
            out Span<byte> encryptionKey);

        var exportResult = validatedSecretKey.TryExportPrivateKey(encryptionKey, out var exportBytesWritten);
        Debug.Assert(exportResult && exportBytesWritten == validatedSecretKey.KeySizeBytes);

        return KeyedHashFunction(encryptionKey, inputData, signature, out bytesWritten);
    }

    /// <inheritdoc />
    public override bool Verify(SecretKey secretKey, ReadOnlySpan<byte> inputData, ReadOnlySpan<byte> signature)
    {
        if (signature.Length != SignatureSizeBytes)
            return false;

        var computedSignature = SignatureSizeBytes <= JoseConstants.MaxStackAlloc ?
            stackalloc byte[SignatureSizeBytes] :
            GC.AllocateUninitializedArray<byte>(SignatureSizeBytes, pinned: false);

        return TrySign(secretKey, inputData, computedSignature, out _) &&
               CryptographicOperations.FixedTimeEquals(computedSignature, signature);
    }
}
