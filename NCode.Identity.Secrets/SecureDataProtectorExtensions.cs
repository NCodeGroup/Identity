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
using NCode.CryptoMemory;
using NCode.Identity.DataProtection;

namespace NCode.Identity.Secrets;

/// <summary>
/// Provides extension methods for <see cref="ISecureDataProtector"/>.
/// </summary>
public static class SecureDataProtectorExtensions
{
    /// <summary>
    /// Cryptographically unprotects a piece of protected data.
    /// </summary>
    /// <param name="dataProtector">The <see cref="ISecureDataProtector"/> instance.</param>
    /// <param name="protectedBytes">The protected data to unprotect.</param>
    /// <param name="plaintext">Destination for the plaintext data of the protected data.</param>
    /// <returns>An <see cref="IDisposable"/> that controls the lifetime of the plaintext data from a memory pool.</returns>
    public static IDisposable Unprotect(
        this ISecureDataProtector dataProtector,
        byte[] protectedBytes,
        out Span<byte> plaintext
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
                        out var bytesWritten))
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

    /// <summary>
    /// Cryptographically unprotects asymmetric key material that is <c>PKCS#8</c> encoded.
    /// </summary>
    /// <param name="dataProtector">The <see cref="ISecureDataProtector"/> instance.</param>
    /// <param name="protectedPkcs8PrivateKey">The protected asymmetric key material that is <c>PKCS#8</c> encoded.</param>
    /// <param name="algorithmFactory">Factory method that can be used to create <typeparamref name="T"/> instances.</param>
    /// <typeparam name="T">The type of the <see cref="AsymmetricAlgorithm"/>.</typeparam>
    /// <returns>The newly created asymmetric key initialized with it's corresponding key material.</returns>
    public static T ExportAsymmetricAlgorithm<T>(
        this ISecureDataProtector dataProtector,
        byte[] protectedPkcs8PrivateKey,
        Func<T> algorithmFactory
    )
        where T : AsymmetricAlgorithm
    {
        using var _ = dataProtector.Unprotect(
            protectedPkcs8PrivateKey,
            out var pkcs8PrivateKey);

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
