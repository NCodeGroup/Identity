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

using System.Buffers;
using System.Security.Cryptography;
using NIdentity.OpenId.Cryptography.Binary;
using NIdentity.OpenId.Cryptography.Descriptors;

namespace NIdentity.OpenId.Cryptography.Keys;

/// <summary>
/// Provides a <see cref="SecretKey"/> implementation for the <c>symmetric</c> cryptographic keys.
/// </summary>
public class SharedSecretKey : SecretKey, ISupportKeySize
{
    /// <summary>
    /// Gets a singleton instance for <see cref="RandomNumberGenerator"/>.
    /// </summary>
    public static RandomNumberGenerator RandomNumberGenerator { get; } = RandomNumberGenerator.Create();

    /// <summary>
    /// Generates and returns a new <see cref="SharedSecretKey"/> with random key material for the specified algorithm and optional hint for the key size.
    /// </summary>
    /// <param name="descriptor">The <see cref="AlgorithmDescriptor"/> that describes for what algorithm to generate a new cryptographic key.</param>
    /// <param name="keyBitLengthHint">An optional value that specifies the key size in bits to generate.
    /// This value is verified against the legal key sizes for the algorithm.
    /// If omitted, the first legal key size is used.</param>
    /// <returns>The newly generated <see cref="SharedSecretKey"/>.</returns>
    public static SharedSecretKey GenerateNewKey(AlgorithmDescriptor descriptor, int? keyBitLengthHint = default)
    {
        var keySizeBytes = GetKeySizeBytes(descriptor, keyBitLengthHint);

        IMemoryOwner<byte> memoryOwner = new HeapMemoryManager(keySizeBytes);
        try
        {
            RandomNumberGenerator.GetBytes(memoryOwner.Memory.Span);
            return new SharedSecretKey(memoryOwner);
        }
        catch
        {
            memoryOwner.Dispose();
            throw;
        }
    }

    private static int GetKeySizeBytes(AlgorithmDescriptor descriptor, int? keyBitLengthHint = default)
    {
        int keySizeBits;
        if (descriptor is ISupportLegalSizes supportLegalSizes)
        {
            keySizeBits = KeySizesUtility.GetLegalSize(keyBitLengthHint, supportLegalSizes.LegalSizes);
        }
        else
        {
            const int defaultSizeBits = 128;
            keySizeBits = keyBitLengthHint ?? defaultSizeBits;
        }

        return keySizeBits / BinaryUtility.BitsPerByte;
    }

    private IMemoryOwner<byte> MemoryOwner { get; }

    /// <inheritdoc />
    public int KeyBitLength => KeyByteLength * BinaryUtility.BitsPerByte;

    /// <summary>
    /// Gets the length in bytes of the key material.
    /// </summary>
    public int KeyByteLength => MemoryOwner.Memory.Length;

    /// <summary>
    /// Gets the key material.
    /// </summary>
    public ReadOnlySpan<byte> KeyBytes => MemoryOwner.Memory.Span;

    /// <summary>
    /// Initializes a new instance of the <see cref="SharedSecretKey"/> with the specified <see cref="IMemoryOwner{T}"/> containing the key material.
    /// </summary>
    /// <param name="memoryOwner"></param>
    public SharedSecretKey(IMemoryOwner<byte> memoryOwner)
    {
        MemoryOwner = memoryOwner;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            MemoryOwner.Dispose();
        }

        base.Dispose(disposing);
    }
}
