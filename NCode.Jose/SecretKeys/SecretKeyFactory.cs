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

using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using NCode.CryptoMemory;

namespace NCode.Jose.SecretKeys;

/// <summary>
/// Provides a default implementation of the <see cref="ISecretKeyFactory"/> abstraction.
/// </summary>
public class SecretKeyFactory : ISecretKeyFactory
{
    private static X509Certificate2? RemovePrivateKey(X509Certificate2? certificate)
    {
        if (certificate is null)
            return certificate;
        if (!certificate.HasPrivateKey)
            return certificate;

        var publicBytes = certificate.Export(X509ContentType.Cert);
        certificate.Dispose();

        certificate = new X509Certificate2(publicBytes);
        Debug.Assert(!certificate.HasPrivateKey);

        return certificate;
    }

    /// <inheritdoc />
    public SecretKey Create(KeyMetadata metadata, X509Certificate2 certificate)
    {
        if (!certificate.HasPrivateKey)
            throw new ArgumentException("The certificate does not contain a private key.", nameof(certificate));

        var newMetadata = metadata with
        {
            KeyId = metadata.KeyId ?? certificate.Thumbprint,
            ExpiresWhen = metadata.ExpiresWhen ?? certificate.NotAfter
        };

        using var rsa = certificate.GetRSAPrivateKey();
        if (rsa != null)
            return CreateRsa(newMetadata, rsa, certificate);

        using var ecDsa = certificate.GetECDsaPrivateKey();
        if (ecDsa != null)
            return CreateEcc(newMetadata, ecDsa, certificate);

        using var ecDiffieHellman = certificate.GetECDiffieHellmanPrivateKey();
        if (ecDiffieHellman != null)
            return CreateEcc(newMetadata, ecDiffieHellman, certificate);

        var algorithm = certificate.GetKeyAlgorithm();
        throw new NotSupportedException($"The certificate uses a key algorithm '{algorithm}' that is not supported.");
    }

    #region RSA

    /// <inheritdoc />
    public RsaSecretKey CreateRsa(KeyMetadata metadata, RSA key, X509Certificate2? certificate = null) =>
        CreateAsymmetric<RsaSecretKey>(key, bytes => new DefaultRsaSecretKey(
            metadata,
            key.KeySize,
            bytes,
            RemovePrivateKey(certificate)));

    /// <inheritdoc />
    public RsaSecretKey CreateRsaPem(KeyMetadata metadata, ReadOnlySpan<char> chars)
    {
        using var key = RSA.Create();
        key.ImportFromPem(chars);
        return CreateRsa(metadata, key);
    }

    /// <inheritdoc />
    public RsaSecretKey CreateRsaPkcs8(KeyMetadata metadata, ReadOnlySpan<byte> bytes)
    {
        using var key = RSA.Create();
        key.ImportPkcs8PrivateKey(bytes, out var bytesRead);
        Debug.Assert(bytesRead == bytes.Length);
        return CreateRsa(metadata, key);
    }

    #endregion

    #region ECC

    /// <inheritdoc />
    public EccSecretKey CreateEcc(KeyMetadata metadata, ECDsa key, X509Certificate2? certificate = null) =>
        CreateAsymmetric<EccSecretKey>(key, bytes => new DefaultEccSecretKey(
            metadata,
            key.KeySize,
            bytes,
            RemovePrivateKey(certificate)));

    /// <inheritdoc />
    public EccSecretKey CreateEcc(KeyMetadata metadata, ECDiffieHellman key, X509Certificate2? certificate = null) =>
        CreateAsymmetric<EccSecretKey>(key, bytes => new DefaultEccSecretKey(
            metadata,
            key.KeySize,
            bytes,
            RemovePrivateKey(certificate)));

    /// <inheritdoc />
    public EccSecretKey CreateEccPem(KeyMetadata metadata, ReadOnlySpan<char> chars)
    {
        using var key = ECDiffieHellman.Create();
        key.ImportFromPem(chars);
        return CreateEcc(metadata, key);
    }

    /// <inheritdoc />
    public EccSecretKey CreateEccPkcs8(KeyMetadata metadata, ReadOnlySpan<byte> bytes)
    {
        using var key = ECDiffieHellman.Create();
        key.ImportPkcs8PrivateKey(bytes, out var bytesRead);
        Debug.Assert(bytesRead == bytes.Length);
        return CreateEcc(metadata, key);
    }

    #endregion

    #region Symmetric

    /// <inheritdoc />
    public SymmetricSecretKey CreateSymmetric(KeyMetadata metadata, ReadOnlySpan<byte> bytes) =>
        new DefaultSymmetricSecretKey(metadata, bytes);

    /// <inheritdoc />
    public SymmetricSecretKey CreateSymmetric(KeyMetadata metadata, ReadOnlySpan<char> password)
    {
        var byteCount = Encoding.UTF8.GetByteCount(password);
        using var _ = CryptoPool.Rent(byteCount, isSensitive: true, out Span<byte> bytes);

        var bytesWritten = Encoding.UTF8.GetBytes(password, bytes);
        Debug.Assert(bytesWritten == byteCount);

        return new DefaultSymmetricSecretKey(metadata, bytes);
    }

    #endregion

    /// <summary>
    /// Defines a callback method to instantiate concrete <see cref="SecretKey"/> instances given cryptographic key material.
    /// </summary>
    /// <typeparam name="T">The concrete <see cref="SecretKey"/> type.</typeparam>
    private delegate T SecretKeyFactoryDelegate<out T>(ReadOnlySpan<byte> keyBytes)
        where T : SecretKey;

    private static T CreateAsymmetric<T>(AsymmetricAlgorithm key, SecretKeyFactoryDelegate<T> factory)
        where T : AsymmetricSecretKey
    {
        var byteCount = 4096;
        while (true)
        {
            using var lease = CryptoPool.Rent(byteCount, isSensitive: true);
            var bytes = lease.Memory.Span;

            if (key.TryExportPkcs8PrivateKey(bytes, out var bytesWritten))
            {
                return factory(bytes[..bytesWritten]);
            }

            byteCount = checked(byteCount * 2);
        }
    }
}
