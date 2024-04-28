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

using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using NCode.CryptoMemory;
using NCode.Identity.DataProtection;

namespace NCode.Identity.Secrets;

/// <summary>
/// Provides a default implementation of the <see cref="ISecretKeyFactory"/> abstraction.
/// </summary>
public class DefaultSecretKeyFactory(
    ISecureDataProtector dataProtector
) : ISecretKeyFactory
{
    private ISecureDataProtector DataProtector { get; } = dataProtector;

    /// <inheritdoc />
    public SecretKey Empty => EmptySecretKey.Singleton;

    #region Symmetric

    private DefaultSymmetricSecretKey CreateSymmetric(
        KeyMetadata metadata,
        int keySizeBytes,
        byte[] protectedPrivateKey
    ) => new(
        DataProtector,
        metadata,
        keySizeBytes,
        protectedPrivateKey);

    /// <inheritdoc />
    public SymmetricSecretKey CreateSymmetric(KeyMetadata metadata, ReadOnlySpan<byte> bytes)
    {
        var keySizeBytes = bytes.Length;
        var protectedPrivateKey = DataProtector.Protect(bytes);
        return CreateSymmetric(metadata, keySizeBytes, protectedPrivateKey);
    }

    /// <inheritdoc />
    public SymmetricSecretKey CreateSymmetric(KeyMetadata metadata, ReadOnlySpan<char> password)
    {
        var keySizeBytes = SecureEncoding.Utf8.GetByteCount(password);
        using var lease = SecureMemoryPool<byte>.Shared.Rent(keySizeBytes);
        var bytesWritten = SecureEncoding.Utf8.GetBytes(password, lease.Memory.Span);
        Debug.Assert(bytesWritten == keySizeBytes);
        return CreateSymmetric(metadata, lease.Memory.Span[..keySizeBytes]);
    }

    #endregion

    #region Asymmetric

    /// <inheritdoc />
    public AsymmetricSecretKey Create(KeyMetadata metadata, X509Certificate2 certificate)
    {
        if (!certificate.HasPrivateKey)
            throw new ArgumentException("The certificate does not contain a private key.", nameof(certificate));

        var newMetadata = metadata with
        {
            KeyId = metadata.KeyId ?? certificate.Thumbprint,
            ExpiresWhen = metadata.ExpiresWhen ?? certificate.NotAfter
        };

        using var ecDiffieHellman = certificate.GetECDiffieHellmanPrivateKey();
        if (ecDiffieHellman != null)
            return CreateEcc(newMetadata, ecDiffieHellman, certificate);

        using var ecDsa = certificate.GetECDsaPrivateKey();
        if (ecDsa != null)
            return CreateEcc(newMetadata, ecDsa, certificate);

        using var rsa = certificate.GetRSAPrivateKey();
        if (rsa != null)
            return CreateRsa(newMetadata, rsa, certificate);

        var algorithm = certificate.GetKeyAlgorithm();
        throw new NotSupportedException($"The certificate uses a key algorithm '{algorithm}' that is not supported.");
    }

    private delegate T AsymmetricSecretKeyFactoryDelegate<out T>(
        byte[] protectedPkcs8PrivateKey,
        byte[]? certificateRawData
    ) where T : AsymmetricSecretKey;

    private T CreateAsymmetric<T>(
        AsymmetricAlgorithm asymmetricAlgorithm,
        X509Certificate? certificate,
        AsymmetricSecretKeyFactoryDelegate<T> factory
    ) where T : AsymmetricSecretKey
    {
        // TODO: should we validate the algorithm from the certificate matches the algorithm from the key?
        var protectedPkcs8PrivateKey = ExportProtectedPkcs8PrivateKey(asymmetricAlgorithm);
        var certificateRawData = certificate?.Export(X509ContentType.Cert); // this will not include the private key
        return factory(protectedPkcs8PrivateKey, certificateRawData);
    }

    private byte[] ExportProtectedPkcs8PrivateKey(AsymmetricAlgorithm asymmetricAlgorithm)
    {
        var byteCount = SecureMemoryPool<byte>.PageSize;
        while (true)
        {
            using var lease = SecureMemoryPool<byte>.Shared.Rent(byteCount);

            if (asymmetricAlgorithm.TryExportPkcs8PrivateKey(lease.Memory.Span, out var bytesWritten))
            {
                return DataProtector.Protect(lease.Memory.Span[..bytesWritten]);
            }

            byteCount = checked(byteCount * 2);
        }
    }

    #endregion

    #region RSA

    private DefaultRsaSecretKey CreateRsa(
        KeyMetadata metadata,
        int modulusSizeBits,
        byte[] protectedPkcs8PrivateKey,
        byte[]? certificateRawData
    ) => new(
        DataProtector,
        metadata,
        modulusSizeBits,
        protectedPkcs8PrivateKey,
        certificateRawData);

    /// <inheritdoc />
    public RsaSecretKey CreateRsa(KeyMetadata metadata, RSA key, X509Certificate2? certificate = null) =>
        CreateAsymmetric<RsaSecretKey>(key, certificate, (keyBytes, certificateBytes) => CreateRsa(
            metadata,
            key.KeySize,
            keyBytes,
            certificateBytes));

    /// <inheritdoc />
    public RsaSecretKey CreateRsaPem(KeyMetadata metadata, ReadOnlySpan<char> chars, X509Certificate2? certificate = null)
    {
        using var key = RSA.Create();
        key.ImportFromPem(chars);
        return CreateRsa(metadata, key, certificate);
    }

    /// <inheritdoc />
    public RsaSecretKey CreateRsaPkcs8(KeyMetadata metadata, ReadOnlySpan<byte> bytes, X509Certificate2? certificate = null)
    {
        using var key = RSA.Create();
        key.ImportPkcs8PrivateKey(bytes, out var bytesRead);
        Debug.Assert(bytesRead == bytes.Length);
        return CreateRsa(metadata, key, certificate);
    }

    #endregion

    #region ECC

    private DefaultEccSecretKey CreateEcc(
        KeyMetadata metadata,
        int curveSizeBits,
        byte[] protectedPkcs8PrivateKey,
        byte[]? certificateRawData
    ) => new(
        DataProtector,
        metadata,
        curveSizeBits,
        protectedPkcs8PrivateKey,
        certificateRawData);

    /// <inheritdoc />
    public EccSecretKey CreateEcc(KeyMetadata metadata, ECAlgorithm key, X509Certificate2? certificate = null) =>
        CreateAsymmetric<EccSecretKey>(key, certificate, (keyBytes, certificateBytes) => CreateEcc(
            metadata,
            key.KeySize,
            keyBytes,
            certificateBytes));

    /// <inheritdoc />
    public EccSecretKey CreateEccPem(KeyMetadata metadata, ReadOnlySpan<char> chars, X509Certificate2? certificate = null)
    {
        using var key = ECDiffieHellman.Create();
        key.ImportFromPem(chars);
        return CreateEcc(metadata, key, certificate);
    }

    /// <inheritdoc />
    public EccSecretKey CreateEccPkcs8(KeyMetadata metadata, ReadOnlySpan<byte> bytes, X509Certificate2? certificate = null)
    {
        using var key = ECDiffieHellman.Create();
        key.ImportPkcs8PrivateKey(bytes, out var bytesRead);
        Debug.Assert(bytesRead == bytes.Length);
        return CreateEcc(metadata, key, certificate);
    }

    #endregion
}
