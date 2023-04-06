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
/// <remarks>
/// This class stores the key material in unmanaged memory so that it is pinned (cannot be moved/copied by the GC).
/// </remarks>
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
    /// If omitted, the first legal key size is used or 128 bits.</param>
    /// <returns>The newly generated <see cref="SharedSecretKey"/>.</returns>
    public static SharedSecretKey GenerateNewKey(AlgorithmDescriptor descriptor, int? keyBitLengthHint = default)
    {
        const int keyBitLengthDefault = 128;
        var keySizeBytes = KeySizesUtility.GetLegalSize(descriptor, keyBitLengthHint, keyBitLengthDefault) / BinaryUtility.BitsPerByte;

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
    /// Initializes a new instance of the <see cref="SharedSecretKey"/> class with the specified <see cref="IMemoryOwner{T}"/>
    /// containing the key material. This class will take ownership of the memory block.
    /// </summary>
    /// <param name="memoryOwner">The cryptographic material for the secret key.</param>
    public SharedSecretKey(IMemoryOwner<byte> memoryOwner)
    {
        MemoryOwner = memoryOwner;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SharedSecretKey"/> class by coping the specified key bytes.
    /// </summary>
    /// <param name="keyBytes">The cryptographic material for the secret key.</param>
    public SharedSecretKey(ReadOnlySpan<byte> keyBytes)
    {
        MemoryOwner = new HeapMemoryManager(keyBytes.Length);
        KeyBytes.CopyTo(MemoryOwner.Memory.Span);
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
