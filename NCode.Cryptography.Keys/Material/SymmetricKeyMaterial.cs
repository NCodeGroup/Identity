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
using NCode.Buffers;

namespace NCode.Cryptography.Keys.Material;

/// <summary>
/// Provides an implementation of <see cref="KeyMaterial"/> that uses key material from a byte array.
/// </summary>
public class SymmetricKeyMaterial : KeyMaterial
{
    private IMemoryOwner<byte> MemoryOwner { get; }

    /// <summary>
    /// Gets a read-only buffer of the key bytes.
    /// </summary>
    public ReadOnlySpan<byte> KeyBytes => MemoryOwner.Memory.Span;

    /// <inheritdoc />
    public override int KeySizeBits => KeySizeBytes << 3;

    /// <inheritdoc />
    public override int KeySizeBytes => KeyBytes.Length;

    /// <summary>
    /// Initializes a new instance of the <see cref="SymmetricKeyMaterial"/> class with the specified key material.
    /// </summary>
    /// <param name="memoryOwner">The cryptographic material for the secret key.</param>
    /// <remarks>
    /// This constructor will take ownership of the memory.
    /// </remarks>
    public SymmetricKeyMaterial(IMemoryOwner<byte> memoryOwner)
    {
        MemoryOwner = memoryOwner;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SymmetricKeyMaterial"/> class with the specified key material.
    /// </summary>
    /// <param name="keyBytes">The cryptographic material for the secret key.</param>
    public SymmetricKeyMaterial(ReadOnlySpan<byte> keyBytes)
    {
        MemoryOwner = new HeapMemoryManager(keyBytes.Length, zeroOnDispose: true);
        try
        {
            keyBytes.CopyTo(MemoryOwner.Memory.Span);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing) MemoryOwner.Dispose();
    }

    /// <inheritdoc />
    public override bool TryExportKey(Span<byte> destination, out int bytesWritten)
    {
        var keyBytes = KeyBytes;
        if (keyBytes.TryCopyTo(destination))
        {
            bytesWritten = keyBytes.Length;
            return true;
        }

        bytesWritten = 0;
        return false;
    }
}
