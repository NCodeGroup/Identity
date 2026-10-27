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
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.Algorithms;

/// <summary>
/// Provides methods for all cryptographic digital signature algorithms.
/// </summary>
[PublicAPI]
public abstract class SignatureAlgorithm : KeyedAlgorithm
{
    /// <inheritdoc />
    public override AlgorithmType Type => AlgorithmType.DigitalSignature;

    /// <summary>
    /// Gets the <see cref="HashAlgorithmName"/> that is used by this digital signature algorithm.
    /// </summary>
    public abstract HashAlgorithmName HashAlgorithmName { get; }

    /// <summary>
    /// Gets the size, in bytes, of the digital signature given the size, in bits, of the signing key.
    /// </summary>
    public abstract int GetSignatureSizeBytes(int keySizeBits);

    /// <summary>
    /// Gets the hash function, and its output size in bytes, that OpenID Connect uses for the token hash claims
    /// (at_hash, c_hash, s_hash) of a token this algorithm signs with the given key. The default uses
    /// <see cref="HashAlgorithmName"/>.
    /// </summary>
    /// <param name="secretKey">Contains the key material for the cryptographic algorithm.</param>
    /// <param name="hashSizeBytes">The output size, in bytes, of the returned hash function.</param>
    /// <returns>The hash function to use for the token hash claims.</returns>
    public virtual HashFunctionDelegate GetTokenHashFunction(
        SecretKey secretKey,
        out int hashSizeBytes
    )
    {
        hashSizeBytes = HashAlgorithmName.GetHashSizeBits() >> 3;
        return HashAlgorithmName.GetHashFunction();
    }

    /// <summary>
    /// Computes a digital signature.
    /// </summary>
    /// <param name="secretKey">Contains the key material for the cryptographic algorithm.</param>
    /// <param name="inputData">Contains the data to sign.</param>
    /// <param name="signature">Destination for the calculated signature.</param>
    /// <param name="bytesWritten">The number of bytes written to <paramref name="signature"/>.</param>
    /// <returns><c>true></c> if there was enough room in <paramref name="signature"/> to copy all computed bytes; otherwise, <c>false</c>.</returns>
    public abstract bool TrySign(
        SecretKey secretKey,
        ReadOnlySpan<byte> inputData,
        Span<byte> signature,
        out int bytesWritten
    );

    /// <summary>
    /// Verifies a digital signature.
    /// </summary>
    /// <param name="secretKey">Contains the key material for the cryptographic algorithm.</param>
    /// <param name="inputData">Contains the data what was signed.</param>
    /// <param name="signature">Contains the digital signature to verify.</param>
    /// <returns><c>true</c> if the computed signature matches the <paramref name="signature"/> parameter; otherwise, <c>false</c>.</returns>
    public abstract bool Verify(
        SecretKey secretKey,
        ReadOnlySpan<byte> inputData,
        ReadOnlySpan<byte> signature
    );
}
