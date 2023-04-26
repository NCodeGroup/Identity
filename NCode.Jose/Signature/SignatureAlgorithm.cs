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
public interface ISignatureAlgorithm : IAlgorithm
{
    /// <summary>
    /// Gets the size, in bits, of the digital signature.
    /// </summary>
    int SignatureSizeBits { get; }

    /// <summary>
    /// When overridden in a derived class, computes a digital signature.
    /// </summary>
    /// <param name="secretKey">Contains the key material for the cryptographic algorithm.</param>
    /// <param name="input">Contains the data to sign.</param>
    /// <param name="signature">Destination for the calculated signature.</param>
    /// <param name="bytesWritten">The number of bytes written to <paramref name="signature"/>.</param>
    /// <returns><c>true></c> if there was enough room in <paramref name="signature"/> to copy all computed bytes; otherwise, <c>false</c>.</returns>
    bool TrySign(SecretKey secretKey, ReadOnlySpan<byte> input, Span<byte> signature, out int bytesWritten);

    /// <summary>
    /// When overridden in a derived class, verifies a digital signature.
    /// </summary>
    /// <param name="secretKey">Contains the key material for the cryptographic algorithm.</param>
    /// <param name="input">Contains the data what was signed.</param>
    /// <param name="signature">Contains the digital signature to compare.</param>
    /// <returns><c>true</c> if the computed signature matches the <paramref name="signature"/> parameter; otherwise, <c>false</c>.</returns>
    bool Verify(SecretKey secretKey, ReadOnlySpan<byte> input, ReadOnlySpan<byte> signature);
}

/// <summary>
/// Base implementation for all cryptographic digital signature algorithms.
/// </summary>
public abstract class SignatureAlgorithm : Algorithm, ISignatureAlgorithm
{
    /// <inheritdoc />
    public override AlgorithmType Type => AlgorithmType.DigitalSignature;

    /// <inheritdoc />
    public abstract int SignatureSizeBits { get; }

    /// <summary>
    /// Gets the size, in bytes, of the digital signature.
    /// </summary>
    public virtual int SignatureSizeBytes => (SignatureSizeBits + 7) >> 3;

    /// <inheritdoc />
    public abstract bool TrySign(SecretKey secretKey, ReadOnlySpan<byte> input, Span<byte> signature, out int bytesWritten);

    /// <inheritdoc />
    public virtual bool Verify(SecretKey secretKey, ReadOnlySpan<byte> input, ReadOnlySpan<byte> signature)
    {
        var byteCount = SignatureSizeBytes;
        if (signature.Length != byteCount)
            return false;

        var expected = byteCount <= JoseConstants.MaxStackAlloc ?
            stackalloc byte[byteCount] :
            GC.AllocateUninitializedArray<byte>(byteCount, pinned: false);

        return TrySign(secretKey, input, expected, out _) &&
               CryptographicOperations.FixedTimeEquals(expected, signature);
    }
}
