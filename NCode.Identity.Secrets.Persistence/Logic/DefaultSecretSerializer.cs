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
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.Secrets.Persistence.Logic;

/// <summary>
/// Provides a default implementation for the <see cref="ISecretSerializer"/> abstraction.
/// </summary>
public class DefaultSecretSerializer(
    ISecretKeyFactory secretKeyFactory,
    IEnumerable<ISecretEncoding> secretEncodings
) : ISecretSerializer
{
    private const string ActiveEncodingType = SecretEncodingTypes.Basic;

    private ISecretKeyFactory SecretKeyFactory { get; } = secretKeyFactory;

    private Dictionary<string, ISecretEncoding> SecretEncodings { get; } =
        secretEncodings.ToDictionary(x => x.EncodingType, StringComparer.Ordinal);

    /// <inheritdoc />
    public IReadOnlyCollection<SecretKey> DeserializeSecrets(
        IEnumerable<PersistedSecret> persistedSecrets,
        out bool requiresMigration
    )
    {
        var anyRequiresMigration = false;
        var secretKeys = new SortedSet<SecretKey>(SecretKeyExpiresWhenComparer.Singleton);

        foreach (var persistedSecret in persistedSecrets)
        {
            secretKeys.Add(DeserializeSecret(persistedSecret, out var secretRequiresMigration));
            anyRequiresMigration |= secretRequiresMigration;
        }

        requiresMigration = anyRequiresMigration;
        return secretKeys;
    }

    /// <inheritdoc />
    public SecretKey DeserializeSecret(PersistedSecret persistedSecret, out bool requiresMigration) =>
        persistedSecret.SecretType switch
        {
            SecretTypes.Certificate => DeserializeCertificate(persistedSecret, out requiresMigration),
            SecretTypes.Symmetric => DeserializeSymmetric(persistedSecret, out requiresMigration),
            SecretTypes.Rsa => DeserializeRsa(persistedSecret, out requiresMigration),
            SecretTypes.Ecc => DeserializeEcc(persistedSecret, out requiresMigration),
            _ => throw new InvalidOperationException($"The '{persistedSecret.SecretType}' secret type is not supported.")
        };

    private AsymmetricSecretKey DeserializeCertificate(PersistedSecret persistedSecret, out bool requiresMigration) =>
        CreateSecretKey(persistedSecret, CreateUsingCertificate, out requiresMigration);

    private AsymmetricSecretKey CreateUsingCertificate(KeyMetadata metadata, Memory<byte> privateKeyBytes)
    {
        using var certificate = new X509Certificate2(privateKeyBytes.Span);
        return SecretKeyFactory.Create(metadata, certificate);
    }

    private SymmetricSecretKey DeserializeSymmetric(PersistedSecret persistedSecret, out bool requiresMigration) =>
        CreateSecretKey(persistedSecret, CreateUsingSymmetric, out requiresMigration);

    private SymmetricSecretKey CreateUsingSymmetric(KeyMetadata metadata, Memory<byte> privateKeyBytes) =>
        SecretKeyFactory.CreateSymmetric(metadata, privateKeyBytes.Span);

    private RsaSecretKey DeserializeRsa(PersistedSecret persistedSecret, out bool requiresMigration) =>
        CreateSecretKey(persistedSecret, CreateUsingRsa, out requiresMigration);

    private RsaSecretKey CreateUsingRsa(KeyMetadata metadata, Memory<byte> privateKeyBytes) =>
        SecretKeyFactory.CreateRsaPkcs8(metadata, privateKeyBytes.Span);

    private EccSecretKey DeserializeEcc(PersistedSecret persistedSecret, out bool requiresMigration) =>
        CreateSecretKey(persistedSecret, CreateUsingEcc, out requiresMigration);

    private EccSecretKey CreateUsingEcc(KeyMetadata metadata, Memory<byte> privateKeyBytes) =>
        SecretKeyFactory.CreateEccPkcs8(metadata, privateKeyBytes.Span);

    private T CreateSecretKey<T>(
        PersistedSecret persistedSecret,
        Func<KeyMetadata, Memory<byte>, T> factory,
        out bool requiresMigration
    ) where T : SecretKey
    {
        if (!SecretEncodings.TryGetValue(persistedSecret.EncodingType, out var secretEncoding))
        {
            throw new InvalidOperationException($"The '{persistedSecret.EncodingType}' secret encoding type is not supported.");
        }

        var metadata = new KeyMetadata
        {
            KeyId = persistedSecret.SecretId,
            Use = persistedSecret.Use,
            Algorithm = persistedSecret.Algorithm,
            ExpiresWhen = persistedSecret.ExpiresWhen
        };

        var secretKey = secretEncoding.Decode(
            persistedSecret.EncodedValue,
            privateKeyBytes => factory(metadata, privateKeyBytes)
        );

        Debug.Assert(secretKey.KeySizeBits == persistedSecret.KeySizeBits);

        requiresMigration = !string.Equals(
            ActiveEncodingType,
            persistedSecret.EncodingType,
            StringComparison.Ordinal
        );

        return secretKey;
    }
}
