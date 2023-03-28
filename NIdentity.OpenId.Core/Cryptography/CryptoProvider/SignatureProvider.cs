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

using NIdentity.OpenId.Cryptography.Descriptors;

namespace NIdentity.OpenId.Cryptography.CryptoProvider;

/// <summary>
/// Base implementation for all cryptographic digital signature algorithms.
/// </summary>
public abstract class SignatureProvider : IDisposable
{
    /// <summary>
    /// Gets the <see cref="SecretKey"/> containing the key material used by the cryptographic digital signature algorithm.
    /// </summary>
    public SecretKey SecretKey { get; }

    /// <summary>
    /// Gets an <see cref="SignatureAlgorithmDescriptor"/> that describes the cryptographic digital signature algorithm.
    /// </summary>
    public SignatureAlgorithmDescriptor AlgorithmDescriptor { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SignatureProvider"/> class.
    /// </summary>
    /// <param name="secretKey">Contains the key material used by the cryptographic digital signature algorithm.</param>
    /// <param name="descriptor">Contains an <see cref="SignatureAlgorithmDescriptor"/> that describes the cryptographic digital signature algorithm.</param>
    protected SignatureProvider(SecretKey secretKey, SignatureAlgorithmDescriptor descriptor)
    {
        SecretKey = secretKey;
        AlgorithmDescriptor = descriptor;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// When overridden in a derived class, releases the unmanaged resources used by the
    /// <see cref="SignatureProvider"/>, and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c>
    /// to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        // nothing
    }

    /// <summary>
    /// When overridden in a derived class, computes a digital signature.
    /// </summary>
    /// <param name="input">Contains the data to sign.</param>
    /// <param name="signature">Destination for the calculated signature.</param>
    /// <param name="bytesWritten">The number of bytes written to <paramref name="signature"/>.</param>
    /// <returns><c>true></c> if there was enough room in <paramref name="signature"/> to copy all computed bytes; otherwise, <c>false</c>.</returns>
    public abstract bool TrySign(ReadOnlySpan<byte> input, Span<byte> signature, out int bytesWritten);

    /// <summary>
    /// When overridden in a derived class, verifies a digital signature.
    /// </summary>
    /// <param name="input">Contains the data what was signed.</param>
    /// <param name="signature">Contains the digital signature to compare.</param>
    /// <returns><c>true</c> if the computed signature matches the <paramref name="signature"/> parameter; otherwise, <c>false</c>.</returns>
    public virtual bool Verify(ReadOnlySpan<byte> input, ReadOnlySpan<byte> signature)
    {
        var hashByteLength = AlgorithmDescriptor.HashBitLength;
        if (signature.Length != hashByteLength)
            return false;

        var expected = hashByteLength <= BinaryUtility.StackAllocMax ?
            stackalloc byte[hashByteLength] :
            new byte[hashByteLength];

        if (!TrySign(input, expected, out var bytesWritten))
            return false;

        if (bytesWritten != hashByteLength)
            return false;

        return signature.SequenceEqual(expected);
    }
}
