#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace NCode.Jose.DataProtection;

// https://devblogs.microsoft.com/dotnet/internals-of-the-poh/

/// <summary>
/// Provides a default implementation of the <see cref="ISecureDataProtector"/> abstraction.
/// </summary>
public class DefaultSecureDataProtector(
    IPersistedDataProtector dataProtector
) : ISecureDataProtector
{
    private IPersistedDataProtector DataProtector { get; } = dataProtector;

    /// <inheritdoc />
    public byte[] Protect(ReadOnlySpan<byte> plaintext)
    {
        // pin the plaintextBytes to prevent the GC from moving it around
        var plaintextBytes = GC.AllocateUninitializedArray<byte>(plaintext.Length, pinned: true);
        try
        {
            plaintext.CopyTo(plaintextBytes);
            return DataProtector.Protect(plaintextBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintextBytes);
        }
    }

    /// <inheritdoc />
    public bool TryUnprotect(
        byte[] protectedBytes,
        Span<byte> plaintext,
        out int bytesWritten,
        out bool requiresMigration)
    {
        var plaintextBytes = DataProtector.DangerousUnprotect(
            protectedBytes,
            ignoreRevocationErrors: false,
            out requiresMigration,
            out _);

        // pin the plaintextBytes to prevent the GC from moving it around
        var plaintextHandle = GCHandle.Alloc(plaintextBytes, GCHandleType.Pinned);
        try
        {
            var result = plaintextBytes.AsSpan().TryCopyTo(plaintext);
            bytesWritten = result ? plaintextBytes.Length : 0;
            return result;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintextBytes);
            plaintextHandle.Free();
        }
    }
}
