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
using NIdentity.OpenId.Cryptography.Binary;

namespace NIdentity.OpenId.Cryptography.Keys;

/// <summary>
/// Base class for all secret keys using <see cref="AsymmetricAlgorithm"/>.
/// </summary>
/// <remarks>
/// This class stores the key material in unmanaged memory so that it is pinned (cannot be moved/copied by the GC).
/// </remarks>
public abstract class AsymmetricSecretKey : SecretKey
{
    private IMemoryOwner<byte> MemoryOwner { get; }

    /// <summary>
    /// Gets the cryptographic material for the secret key formatted as <c>PKCS#8</c>.
    /// </summary>
    protected ReadOnlySpan<byte> Pkcs8PrivateKey => MemoryOwner.Memory.Span;

    /// <summary>
    /// Gets the optional <see cref="X509Certificate2"/> for this secret key.
    /// </summary>
    /// <remarks>
    /// This certificate only contains the public portion.
    /// To create a certificate with both the private and public portions, use <c>CopyWithPrivateKey</c>.
    /// Doing so will create certificates with ephemeral keys and not persist keys to disk.
    /// </remarks>
    public X509Certificate2? Certificate { get; }

    /// <inheritdoc />
    public override int KeySizeBits { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AsymmetricSecretKey"/> class.
    /// </summary>
    /// <param name="keyId">The <c>Key ID (KID)</c> for the secret key.</param>
    /// <param name="keySizeBits">The length of the key material in bits.</param>
    /// <param name="pkcs8PrivateKey">The bytes of the key material formatted as <c>PKCS#8</c>.</param>
    /// <param name="certificate">The optional <see cref="X509Certificate2"/> for the secret key.</param>
    protected AsymmetricSecretKey(string keyId, int keySizeBits, ReadOnlySpan<byte> pkcs8PrivateKey, X509Certificate2? certificate = null)
        : base(keyId)
    {
        Debug.Assert(certificate is not { HasPrivateKey: true });

        Certificate = certificate;
        KeySizeBits = keySizeBits;

        MemoryOwner = new HeapMemoryManager(pkcs8PrivateKey.Length);
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
        if (disposing)
        {
            Certificate?.Dispose();
            MemoryOwner.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Factory method to create and initialize an <see cref="AsymmetricAlgorithm"/> instance using the current <c>PKCS#8</c> key material.
    /// </summary>
    /// <param name="factory">The factory method to create an instance of <typeparamref name="T"/>.</param>
    /// <typeparam name="T">The newly initialized instance of <typeparamref name="T"/>.</typeparam>
    /// <returns>The cryptographic algorithm that derives from <see cref="AsymmetricAlgorithm"/>.</returns>
    protected T CreateAsymmetricAlgorithm<T>(Func<T> factory)
        where T : AsymmetricAlgorithm
    {
        var algorithm = factory();

        try
        {
            algorithm.ImportPkcs8PrivateKey(Pkcs8PrivateKey, out _);
        }
        catch
        {
            algorithm.Dispose();
            throw;
        }

        return algorithm;
    }
}
