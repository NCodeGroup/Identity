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

using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using JetBrains.Annotations;
using Microsoft.AspNetCore.DataProtection;
using NCode.CryptoMemory;

namespace NCode.Identity.Secrets;

/// <summary>
/// Provides extension methods for <see cref="IDataProtector"/>.
/// </summary>
[PublicAPI]
public static class DataProtectorExtensions
{
    /// <param name="dataProtector">The <see cref="IDataProtector"/> instance.</param>
    extension(IDataProtector dataProtector)
    {
        /// <summary>
        /// Cryptographically protects a piece of plaintext data and writes the result to a buffer writer.
        /// </summary>
        /// <typeparam name="TWriter">The type of buffer writer to write the protected data to.</typeparam>
        /// <param name="plaintext">The plaintext data to protect.</param>
        /// <param name="destination">The buffer writer to which the protected data will be written.</param>
        /// <remarks>
        /// <para>
        /// This method provides an optimized, streaming alternative to <see cref="IDataProtector.Protect(byte[])"/>.
        /// Rather than allocating an intermediate buffer, the protected data is written directly to the provided
        /// buffer writer, which can improve performance and reduce memory allocation pressure.
        /// </para>
        /// <para>
        /// The buffer writer is advanced by the total number of bytes written to it.
        /// </para>
        /// <para>
        /// When a native <c>ISpanDataProtector</c> is not available, the fallback implementation
        /// pins the plaintext in memory to prevent garbage collection from moving it, and securely
        /// clears the temporary buffer after use using <see cref="CryptographicOperations.ZeroMemory"/>.
        /// </para>
        /// </remarks>
        public void ProtectSpan<TWriter>(ReadOnlySpan<byte> plaintext, ref TWriter destination)
            where TWriter : IBufferWriter<byte>, allows ref struct
        {
#if NET11_0_OR_GREATER
        if (dataProtector is ISpanDataProtector spanProtector)
        {
            spanProtector.Protect(plaintext, ref destination);
        }
        else
#endif
            {
                // pin the plaintext bytes to prevent the GC from moving it around
                // can't use ArrayPool with GCHandle because it doesn't guarantee to return an exact size
                // and data protector doesn't support span (yet)
                using var plaintextBytes = SecureMemoryFactory.CreatePinnedArray(plaintext.Length);

                plaintext.CopyTo(plaintextBytes);
                var protectedBytes = dataProtector.Protect(plaintextBytes);
                var protectedLength = protectedBytes.Length;
                var destinationSpan = destination.GetSpan(protectedLength);
                protectedBytes.CopyTo(destinationSpan);
                destination.Advance(protectedLength);
            }
        }

        /// <summary>
        /// Cryptographically unprotects a piece of protected data and writes the result to a buffer writer.
        /// </summary>
        /// <typeparam name="TWriter">The type of buffer writer to write the unprotected data to.</typeparam>
        /// <param name="protectedData">The protected data to unprotect.</param>
        /// <param name="destination">The buffer writer to which the unprotected plaintext will be written.</param>
        /// <remarks>
        /// <para>
        /// This method provides an optimized, streaming alternative to <see cref="IDataProtector.Unprotect(byte[])"/>.
        /// Rather than allocating an intermediate buffer, the unprotected plaintext is written directly to the provided
        /// buffer writer, which can improve performance and reduce memory allocation pressure.
        /// </para>
        /// <para>
        /// The buffer writer is advanced by the total number of bytes written to it.
        /// </para>
        /// <para>
        /// When a native <c>ISpanDataProtector</c> is not available, the fallback implementation
        /// pins the plaintext in memory to prevent garbage collection from moving it, and securely
        /// clears the temporary buffer after use using <see cref="CryptographicOperations.ZeroMemory"/>.
        /// </para>
        /// </remarks>
        public void UnprotectSpan<TWriter>(ReadOnlySpan<byte> protectedData, ref TWriter destination)
            where TWriter : IBufferWriter<byte>, allows ref struct
        {
#if NET11_0_OR_GREATER
            if (dataProtector is ISpanDataProtector spanProtector)
            {
                spanProtector.Unprotect(protectedData, ref destination);
            }
            else
#endif
            {
                var plaintextBytes = dataProtector.Unprotect(protectedData.ToArray());

                // pin the plaintextBytes quickly in order to prevent the GC from moving it around
                var plaintextHandle = GCHandle.Alloc(plaintextBytes, GCHandleType.Pinned);
                try
                {
                    var plaintextLength = plaintextBytes.Length;
                    var destinationSpan = destination.GetSpan(plaintextLength);
                    plaintextBytes.CopyTo(destinationSpan);
                    destination.Advance(plaintextLength);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(plaintextBytes);
                    plaintextHandle.Free();
                }
            }
        }

        /// <summary>
        /// Cryptographically unprotects asymmetric key material that is <c>PKCS#8</c> encoded.
        /// </summary>
        /// <param name="protectedPkcs8PrivateKey">The protected asymmetric key material that is <c>PKCS#8</c> encoded.</param>
        /// <param name="algorithmFactory">Factory method that can be used to create <typeparamref name="T"/> instances.</param>
        /// <typeparam name="T">The type of the <see cref="AsymmetricAlgorithm"/>.</typeparam>
        /// <returns>The newly created asymmetric key initialized with it's corresponding key material.</returns>
        public T ExportAsymmetricAlgorithm<T>(byte[] protectedPkcs8PrivateKey, Func<T> algorithmFactory)
            where T : AsymmetricAlgorithm
        {
            using var privateKeyBuffer = SecureMemoryFactory.CreateSecureBuffer();
            IBufferWriter<byte> privateKeyWriter = privateKeyBuffer;

            dataProtector.UnprotectSpan(protectedPkcs8PrivateKey, ref privateKeyWriter);

            using var spanLease = privateKeyBuffer.Sequence.GetSpanLease(isSensitive: true);

            var algorithm = algorithmFactory();
            try
            {
                algorithm.ImportPkcs8PrivateKey(spanLease.Span, out var bytesRead);
                Debug.Assert(bytesRead == spanLease.Span.Length);
            }
            catch
            {
                algorithm.Dispose();
                throw;
            }

            return algorithm;
        }
    }
}
