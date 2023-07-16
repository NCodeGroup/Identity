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
    public override int SignatureSizeBits { get; }

    private KeyedHashFunctionDelegate KeyedHashFunction { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="KeyedHashSignatureAlgorithm"/> class.
    /// </summary>
    /// <param name="code">Contains a <see cref="string"/> value that uniquely identifies the cryptographic algorithm.</param>
    /// <param name="signatureSizeBits">Contains the size, in bits, of the digital signature.</param>
    /// <param name="keyedHashFunction">Contains a delegate for the <c>keyed hash (HMAC)</c> function to use.</param>
    public KeyedHashSignatureAlgorithm(string code, int signatureSizeBits, KeyedHashFunctionDelegate keyedHashFunction)
    {
        Code = code;
        SignatureSizeBits = signatureSizeBits;
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
    public override bool TrySign(SecretKey secretKey, ReadOnlySpan<byte> inputData, Span<byte> signature, out int bytesWritten)
    {
        var validatedSecurityKey = ValidateSecretKey<SymmetricSecretKey>(secretKey);

        return KeyedHashFunction(validatedSecurityKey.KeyBytes, inputData, signature, out bytesWritten);
    }
}
