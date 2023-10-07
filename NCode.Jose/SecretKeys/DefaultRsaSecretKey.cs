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
using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using NCode.CryptoMemory;
using NCode.Jose.Extensions;

namespace NCode.Jose.SecretKeys;

/// <summary>
/// Provides a default implementation of the <see cref="RsaSecretKey"/> abstraction.
/// </summary>
/// <remarks>
/// This implementation stores the key material in unmanaged memory so that it is pinned (cannot be moved/copied by the GC).
/// </remarks>
public class DefaultRsaSecretKey : RsaSecretKey
{
    /// <inheritdoc />
    public override KeyMetadata Metadata { get; }

    /// <inheritdoc />
    public override int KeySizeBits { get; }

    /// <inheritdoc />
    public override ReadOnlySpan<byte> Pkcs8PrivateKey => MemoryOwner.Memory.Span;

    /// <inheritdoc />
    public override X509Certificate2? Certificate { get; }

    private IMemoryOwner<byte> MemoryOwner { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RsaSecretKey"/> class with the specified <c>PKCS#8</c> key material
    /// and optional certificate.
    /// </summary>
    /// <param name="metadata">The metadata for the secret key.</param>
    /// <param name="keySizeBits">The length of the key material in bits.</param>
    /// <param name="pkcs8PrivateKey">The bytes of the key material formatted as <c>PKCS#8</c>.</param>
    /// <param name="certificate">The optional <see cref="X509Certificate2"/> for the secret key.</param>
    public DefaultRsaSecretKey(KeyMetadata metadata, int keySizeBits, ReadOnlySpan<byte> pkcs8PrivateKey, X509Certificate2? certificate = null)
    {
        Debug.Assert(certificate == null || (certificate.GetKeyAlgorithm() == Oid && !certificate.HasPrivateKey));

        Metadata = metadata;
        KeySizeBits = keySizeBits;
        Certificate = certificate;

        MemoryOwner = new HeapMemoryManager(pkcs8PrivateKey.Length, zeroOnDispose: true);
        try
        {
            pkcs8PrivateKey.CopyTo(MemoryOwner.Memory.Span);
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
        Certificate?.Dispose();
        MemoryOwner.Dispose();
    }

    /// <inheritdoc />
    public override RSA ExportRSA() => this.ExportAlgorithm(RSA.Create);
}
