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
/// Provides methods for all cryptographic digital signature algorithms.
/// </summary>
public interface ISignatureAlgorithm : IKeyedAlgorithm
{
    /// <summary>
    /// Gets the size, in bytes, of the digital signature given the size, in bits, of the signing key.
    /// </summary>
    int GetSignatureSizeBytes(int keySizeBits);

    /// <summary>
    /// When overridden in a derived class, computes a digital signature.
    /// </summary>
    /// <param name="secretKey">Contains the key material for the cryptographic algorithm.</param>
    /// <param name="inputData">Contains the data to sign.</param>
    /// <param name="signature">Destination for the calculated signature.</param>
    /// <param name="bytesWritten">The number of bytes written to <paramref name="signature"/>.</param>
    /// <returns><c>true></c> if there was enough room in <paramref name="signature"/> to copy all computed bytes; otherwise, <c>false</c>.</returns>
    bool TrySign(SecretKey secretKey, ReadOnlySpan<byte> inputData, Span<byte> signature, out int bytesWritten);

    /// <summary>
    /// When overridden in a derived class, verifies a digital signature.
    /// </summary>
    /// <param name="secretKey">Contains the key material for the cryptographic algorithm.</param>
    /// <param name="inputData">Contains the data what was signed.</param>
    /// <param name="signature">Contains the digital signature to compare.</param>
    /// <returns><c>true</c> if the computed signature matches the <paramref name="signature"/> parameter; otherwise, <c>false</c>.</returns>
    bool Verify(SecretKey secretKey, ReadOnlySpan<byte> inputData, ReadOnlySpan<byte> signature);
}

/// <summary>
/// Base implementation for all cryptographic digital signature algorithms.
/// </summary>
public abstract class SignatureAlgorithm : KeyedAlgorithm, ISignatureAlgorithm
{
    /// <inheritdoc />
    public override AlgorithmType Type => AlgorithmType.DigitalSignature;

    /// <inheritdoc />
    public abstract int GetSignatureSizeBytes(int keySizeBits);

    /// <inheritdoc />
    public abstract bool TrySign(SecretKey secretKey, ReadOnlySpan<byte> inputData, Span<byte> signature, out int bytesWritten);

    /// <inheritdoc />
    public virtual bool Verify(SecretKey secretKey, ReadOnlySpan<byte> inputData, ReadOnlySpan<byte> signature)
    {
        var signatureSizeBytes = GetSignatureSizeBytes(secretKey.KeySizeBits);
        if (signature.Length != signatureSizeBytes)
            return false;

        var computedSignature = signatureSizeBytes <= JoseConstants.MaxStackAlloc ?
            stackalloc byte[signatureSizeBytes] :
            GC.AllocateUninitializedArray<byte>(signatureSizeBytes, pinned: false);

        return TrySign(secretKey, inputData, computedSignature, out _) &&
               CryptographicOperations.FixedTimeEquals(computedSignature, signature);
    }
}
