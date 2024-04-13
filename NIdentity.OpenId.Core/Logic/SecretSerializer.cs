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
using System.Security.Cryptography.X509Certificates;
using NCode.Encoders;
using NCode.Jose.Buffers;
using NCode.Jose.SecretKeys;
using NIdentity.OpenId.DataProtection;
using Secret = NIdentity.OpenId.DataContracts.Secret;

namespace NIdentity.OpenId.Logic;

/// <summary>
/// Provides a default implementation for the <see cref="ISecretSerializer"/> abstraction.
/// </summary>
public class SecretSerializer : ISecretSerializer
{
    private ISecureDataProtector DataProtector { get; }
    private ISecretKeyFactory SecretKeyFactory { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SecretSerializer"/> class.
    /// </summary>
    /// <param name="dataProtector">The <see cref="ISecureDataProtector"/> instance.</param>
    /// <param name="secretKeyFactory">The <see cref="ISecretKeyFactory"/> instance.</param>
    public SecretSerializer(ISecureDataProtector dataProtector, ISecretKeyFactory secretKeyFactory)
    {
        DataProtector = dataProtector;
        SecretKeyFactory = secretKeyFactory;
    }

    /// <inheritdoc />
    public SecretKey DeserializeSecret(Secret secret, out bool requiresMigration)
    {
        var metadata = new KeyMetadata
        {
            TenantId = secret.TenantId,
            KeyId = secret.SecretId,
            Use = secret.Use,
            Algorithm = secret.Algorithm,
            ExpiresWhen = secret.ExpiresWhen
        };
        return secret.SecretType switch
        {
            SecretConstants.SecretTypes.Certificate => DeserializeCertificate(metadata, secret, out requiresMigration),
            SecretConstants.SecretTypes.Symmetric => DeserializeSymmetric(metadata, secret, out requiresMigration),
            SecretConstants.SecretTypes.Rsa => DeserializeRsa(metadata, secret, out requiresMigration),
            SecretConstants.SecretTypes.Ecc => DeserializeEcc(metadata, secret, out requiresMigration),
            _ => throw new InvalidOperationException($"The '{secret.SecretType}' secret type is not supported.")
        };
    }

    private SecretKey DeserializeCertificate(KeyMetadata metadata, Secret secret, out bool requiresMigration)
    {
        var protectedBytes = Base64Url.Decode(secret.ProtectedValue);
        using var lease = SecureMemoryPool<byte>.Shared.Rent(secret.UnprotectedSizeBytes);

        var unprotectResult = DataProtector.TryUnprotect(
            protectedBytes,
            lease.Memory.Span,
            out var bytesWritten,
            out requiresMigration);

        Debug.Assert(unprotectResult && bytesWritten == secret.UnprotectedSizeBytes);
        var certificateBytes = lease.Memory.Span[..bytesWritten];

        var certificate = new X509Certificate2(certificateBytes);
        try
        {
            // fyi, the certificate is owned by the secret
            return SecretKeyFactory.Create(metadata, certificate);
        }
        catch
        {
            certificate.Dispose();
            throw;
        }
    }

    private SymmetricSecretKey DeserializeSymmetric(KeyMetadata metadata, Secret secret, out bool requiresMigration)
    {
        var protectedBytes = Base64Url.Decode(secret.ProtectedValue);
        var lease = SecureMemoryPool<byte>.Shared.Rent(secret.UnprotectedSizeBytes);
        try
        {
            var unprotectResult = DataProtector.TryUnprotect(
                protectedBytes,
                lease.Memory.Span,
                out var bytesWritten,
                out requiresMigration);

            Debug.Assert(unprotectResult && bytesWritten == secret.UnprotectedSizeBytes);
            return SecretKeyFactory.CreateSymmetric(metadata, lease, bytesWritten);
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    private RsaSecretKey DeserializeRsa(KeyMetadata metadata, Secret secret, out bool requiresMigration)
    {
        var protectedBytes = Base64Url.Decode(secret.ProtectedValue);
        var lease = SecureMemoryPool<byte>.Shared.Rent(secret.UnprotectedSizeBytes);
        try
        {
            var unprotectResult = DataProtector.TryUnprotect(
                protectedBytes,
                lease.Memory.Span,
                out var bytesWritten,
                out requiresMigration);

            Debug.Assert(unprotectResult && bytesWritten == secret.UnprotectedSizeBytes);
            return SecretKeyFactory.CreateRsa(metadata, secret.KeySizeBits, lease, bytesWritten);
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    private EccSecretKey DeserializeEcc(KeyMetadata metadata, Secret secret, out bool requiresMigration)
    {
        var protectedBytes = Base64Url.Decode(secret.ProtectedValue);
        var lease = SecureMemoryPool<byte>.Shared.Rent(secret.UnprotectedSizeBytes);
        try
        {
            var unprotectResult = DataProtector.TryUnprotect(
                protectedBytes,
                lease.Memory.Span,
                out var bytesWritten,
                out requiresMigration);

            Debug.Assert(unprotectResult && bytesWritten == secret.UnprotectedSizeBytes);
            return SecretKeyFactory.CreateEcc(metadata, secret.KeySizeBits, lease, bytesWritten);
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }
}
