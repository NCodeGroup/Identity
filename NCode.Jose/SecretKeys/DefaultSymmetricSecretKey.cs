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
using NCode.CryptoMemory;

namespace NCode.Jose.SecretKeys;

/// <summary>
/// Provides a default implementation for the <see cref="SymmetricSecretKey"/> abstraction.
/// </summary>
/// <remarks>
/// This implementation stores the key material in unmanaged memory so that it is pinned (cannot be moved/copied by the GC).
/// </remarks>
public class DefaultSymmetricSecretKey : SymmetricSecretKey
{
    /// <inheritdoc />
    public override KeyMetadata Metadata { get; }

    /// <inheritdoc />
    public override int KeySizeBits => KeySizeBytes << 3;

    /// <inheritdoc />
    public override int KeySizeBytes => MemoryOwner.Memory.Length;

    /// <inheritdoc />
    public override ReadOnlySpan<byte> KeyBytes => MemoryOwner.Memory.Span;

    private IMemoryOwner<byte> MemoryOwner { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SymmetricSecretKey"/> class by coping the specified key bytes.
    /// </summary>
    /// <param name="metadata">The metadata for the secret key.</param>
    /// <param name="keyBytes">The cryptographic material for the secret key.</param>
    public DefaultSymmetricSecretKey(KeyMetadata metadata, ReadOnlySpan<byte> keyBytes)
    {
        Metadata = metadata;

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
        if (!disposing) return;
        MemoryOwner.Dispose();
    }
}
