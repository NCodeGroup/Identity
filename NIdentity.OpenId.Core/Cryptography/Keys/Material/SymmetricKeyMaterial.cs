﻿#region Copyright Preamble

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
using NIdentity.OpenId.Cryptography.Binary;

namespace NIdentity.OpenId.Cryptography.Keys.Material;

/// <summary>
/// Provides an implementation of <see cref="KeyMaterial"/> that uses key material from a byte array.
/// </summary>
public class SymmetricKeyMaterial : KeyMaterial
{
    private Memory<byte> MemorySource { get; }

    /// <inheritdoc />
    public override int KeySizeBits => MemorySource.Length / BinaryUtility.BitsPerByte;

    /// <summary>
    /// Gets a read-only buffer of the key bytes.
    /// </summary>
    public ReadOnlySpan<byte> KeyBytes => MemorySource.Span;

    /// <summary>
    /// Initializes a new instance of the <see cref="SymmetricKeyMaterial"/> class with the specified key material.
    /// </summary>
    /// <param name="memorySource">An byte array containing the key material.</param>
    public SymmetricKeyMaterial(Memory<byte> memorySource) =>
        MemorySource = memorySource;

    /// <inheritdoc />
    public override void Dispose()
    {
        CryptographicOperations.ZeroMemory(MemorySource.Span);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public override bool TryExportKey(Span<byte> destination, out int bytesWritten)
    {
        if (MemorySource.Span.TryCopyTo(destination))
        {
            bytesWritten = MemorySource.Length;
            return true;
        }

        bytesWritten = 0;
        return false;
    }
}
