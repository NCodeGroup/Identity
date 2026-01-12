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
using System.Security.Cryptography;
using JetBrains.Annotations;
using Microsoft.AspNetCore.DataProtection;
using NCode.CryptoMemory;
using NCode.Identity.DataProtection;

namespace NCode.Identity.Secrets;

/// <summary>
/// Provides extension methods for <see cref="IDataProtector"/>.
/// </summary>
[PublicAPI]
public static class DataProtectorExtensions
{
    /// <summary>
    /// Cryptographically unprotects asymmetric key material that is <c>PKCS#8</c> encoded.
    /// </summary>
    /// <param name="dataProtector">The <see cref="IDataProtector"/> instance.</param>
    /// <param name="protectedPkcs8PrivateKey">The protected asymmetric key material that is <c>PKCS#8</c> encoded.</param>
    /// <param name="algorithmFactory">Factory method that can be used to create <typeparamref name="T"/> instances.</param>
    /// <typeparam name="T">The type of the <see cref="AsymmetricAlgorithm"/>.</typeparam>
    /// <returns>The newly created asymmetric key initialized with it's corresponding key material.</returns>
    public static T ExportAsymmetricAlgorithm<T>(
        this IDataProtector dataProtector,
        byte[] protectedPkcs8PrivateKey,
        Func<T> algorithmFactory
    )
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
