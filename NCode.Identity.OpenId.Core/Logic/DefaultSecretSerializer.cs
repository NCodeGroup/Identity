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
using NCode.Identity.OpenId.Data.Contracts;
using NCode.Jose.Buffers;
using NCode.Jose.DataProtection;
using NCode.Jose.SecretKeys;

namespace NCode.Identity.OpenId.Logic;

/// <summary>
/// Provides a default implementation for the <see cref="ISecretSerializer"/> abstraction.
/// </summary>
public class DefaultSecretSerializer(
    ISecureDataProtector dataProtector,
    ISecretKeyFactory secretKeyFactory
) : ISecretSerializer
{
    private ISecureDataProtector DataProtector { get; } = dataProtector;
    private ISecretKeyFactory SecretKeyFactory { get; } = secretKeyFactory;

    /// <inheritdoc />
    public IReadOnlyCollection<SecretKey> DeserializeSecrets(IEnumerable<Secret> secrets, out bool requiresMigration)
    {
        var anyRequiresMigration = false;
        var results = new SortedSet<SecretKey>(SecretKeyExpiresWhenComparer.Singleton);

        foreach (var secret in secrets)
        {
            results.Add(DeserializeSecret(secret, out var secretRequiresMigration));
            anyRequiresMigration |= secretRequiresMigration;
        }

        requiresMigration = anyRequiresMigration;
        return results;
    }

    /// <inheritdoc />
    public SecretKey DeserializeSecret(Secret secret, out bool requiresMigration) =>
        secret.SecretType switch
        {
            SecretTypes.Certificate => DeserializeCertificate(secret, out requiresMigration),
            SecretTypes.Symmetric => DeserializeSymmetric(secret, out requiresMigration),
            SecretTypes.Rsa => DeserializeRsa(secret, out requiresMigration),
            SecretTypes.Ecc => DeserializeEcc(secret, out requiresMigration),
            _ => throw new InvalidOperationException($"The '{secret.SecretType}' secret type is not supported.")
        };

    private AsymmetricSecretKey DeserializeCertificate(Secret secret, out bool requiresMigration) =>
        CreateSecretKey(secret, CreateUsingCertificate, out requiresMigration);

    private AsymmetricSecretKey CreateUsingCertificate(KeyMetadata metadata, Memory<byte> privateKeyBytes)
    {
        using var certificate = new X509Certificate2(privateKeyBytes.Span);
        return SecretKeyFactory.Create(metadata, certificate);
    }

    private SymmetricSecretKey DeserializeSymmetric(Secret secret, out bool requiresMigration) =>
        CreateSecretKey(secret, CreateUsingSymmetric, out requiresMigration);

    private SymmetricSecretKey CreateUsingSymmetric(KeyMetadata metadata, Memory<byte> privateKeyBytes) =>
        SecretKeyFactory.CreateSymmetric(metadata, privateKeyBytes.Span);

    private RsaSecretKey DeserializeRsa(Secret secret, out bool requiresMigration) =>
        CreateSecretKey(secret, CreateUsingRsa, out requiresMigration);

    private RsaSecretKey CreateUsingRsa(KeyMetadata metadata, Memory<byte> privateKeyBytes) =>
        SecretKeyFactory.CreateRsaPkcs8(metadata, privateKeyBytes.Span);

    private EccSecretKey DeserializeEcc(Secret secret, out bool requiresMigration) =>
        CreateSecretKey(secret, CreateUsingEcc, out requiresMigration);

    private EccSecretKey CreateUsingEcc(KeyMetadata metadata, Memory<byte> privateKeyBytes) =>
        SecretKeyFactory.CreateEccPkcs8(metadata, privateKeyBytes.Span);

    private T CreateSecretKey<T>(
        Secret secret,
        Func<KeyMetadata, Memory<byte>, T> factory,
        out bool requiresMigration
    ) where T : SecretKey
    {
        var metadata = new KeyMetadata
        {
            TenantId = secret.TenantId,
            KeyId = secret.SecretId,
            Use = secret.Use,
            Algorithm = secret.Algorithm,
            ExpiresWhen = secret.ExpiresWhen
        };

        var protectedBytes = Base64Url.Decode(secret.ProtectedValue);

        using var _ = CryptoPool.Rent(
            secret.UnprotectedSizeBytes,
            isSensitive: true,
            out Memory<byte> privateKeyBytes);

        var unprotectResult = DataProtector.TryUnprotect(
            protectedBytes,
            privateKeyBytes.Span,
            out var bytesWritten,
            out requiresMigration);

        Debug.Assert(unprotectResult && bytesWritten == secret.UnprotectedSizeBytes);

        return factory(metadata, privateKeyBytes);
    }
}
