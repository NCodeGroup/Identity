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
using JetBrains.Annotations;
using Microsoft.AspNetCore.DataProtection;
using NCode.CryptoMemory;

namespace NCode.Identity.DataProtection;

/// <summary>
/// Provides an implementation of the <see cref="ISecureDataProtector"/> abstraction that delegates to an <see cref="IDataProtector"/> instance.
/// </summary>
[PublicAPI]
public class DelegatingSecureDataProtector(
    IDataProtector dataProtector
) : ISecureDataProtector
{
    private IDataProtector DataProtector { get; } = dataProtector;

    /// <inheritdoc />
    public ISecureDataProtector CreateProtector(string purpose) =>
        new DelegatingSecureDataProtector(DataProtector.CreateProtector(purpose));

    /// <inheritdoc />
    public byte[] Protect(
        ReadOnlySpan<byte> plaintext)
    {
        // pin the plaintextBytes to prevent the GC from moving it around
        // can't use ArrayPool with GCHandle because it doesn't guarantee to return an exact size
        // and data protector doesn't support span or array segments
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
        ReadOnlySpan<byte> protectedBytes,
        Span<byte> plaintext,
        out int bytesWritten)
    {
        var plaintextBytes = DataProtector.Unprotect(protectedBytes.ToArray());

        // pin the plaintextBytes quickly in order to prevent the GC from moving it around
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

    /// <inheritdoc />
    public IDisposable Unprotect(ReadOnlySpan<byte> protectedBytes, out Span<byte> plaintext)
    {
        var byteCount = SecureMemoryPool<byte>.PageSize;
        while (true)
        {
            var lease = SecureMemoryPool<byte>.Shared.Rent(byteCount);
            try
            {
                var buffer = lease.Memory.Span;
                if (TryUnprotect(protectedBytes, buffer, out var bytesWritten))
                {
                    plaintext = buffer[..bytesWritten];
                    return lease;
                }
            }
            catch
            {
                lease.Dispose();
                throw;
            }

            lease.Dispose();
            byteCount = checked(byteCount * 2);
        }
    }
}
