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

using System.Diagnostics;
using System.Security.Cryptography;
using NCode.Jose.Buffers;

namespace NCode.Jose.DataProtection;

public static class SecureDataProtectorExtensions
{
    public static IDisposable Unprotect(
        this ISecureDataProtector dataProtector,
        byte[] protectedBytes,
        out Span<byte> plaintext,
        out bool requiresMigration
    )
    {
        var byteCount = SecureMemoryPool<byte>.PageSize;
        while (true)
        {
            var lease = SecureMemoryPool<byte>.Shared.Rent(byteCount);
            try
            {
                var buffer = lease.Memory.Span;
                if (dataProtector.TryUnprotect(
                        protectedBytes,
                        buffer,
                        out var bytesWritten,
                        out requiresMigration))
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

    public static T ExportAsymmetricAlgorithm<T>(
        this ISecureDataProtector dataProtector,
        byte[] protectedPkcs8PrivateKey,
        Func<T> algorithmFactory,
        out bool requiresMigration
    )
        where T : AsymmetricAlgorithm
    {
        using var _ = dataProtector.Unprotect(
            protectedPkcs8PrivateKey,
            out var pkcs8PrivateKey,
            out requiresMigration);

        var algorithm = algorithmFactory();
        try
        {
            algorithm.ImportPkcs8PrivateKey(pkcs8PrivateKey, out var bytesRead);
            Debug.Assert(bytesRead == pkcs8PrivateKey.Length);
        }
        catch
        {
            algorithm.Dispose();
            throw;
        }

        return algorithm;
    }
}
