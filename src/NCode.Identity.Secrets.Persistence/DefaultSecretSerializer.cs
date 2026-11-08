// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Diagnostics;
using System.Security.Cryptography;
using NCode.Buffers;
using NCode.Encoders;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Secrets.Persistence.Verifiers;
using NCode.Identity.Secrets.RemoteKeys;
using NCode.Identity.Secrets.Verifiers;

namespace NCode.Identity.Secrets.Persistence;

/// <summary>
/// Provides a default implementation for the <see cref="ISecretSerializer"/> abstraction: a thin persistence wrapper
/// that unprotects the stored key material and delegates decoding to the <see cref="ISecretKeyTypeDescriptor"/> for
/// the secret's type (the single extensibility seam). A stored verifier is read by the registered
/// <see cref="ISecretVerifierScheme"/> its text names. A remote secret is read as the key that its registered
/// <see cref="IRemoteKeyProvider"/> holds, from its stored public key, and is refused when its provider, public key or
/// mapping row is missing.
/// </summary>
// See ADR-1235, ADR-1391 and ADR-1471.
internal class DefaultSecretSerializer(
    ISecretKeyTypeCatalog secretKeyTypeCatalog,
    ISecretVerifierSchemeCatalog secretVerifierSchemeCatalog,
    IRemoteKeyProviderCatalog remoteKeyProviderCatalog
) : ISecretSerializer
{
    private ISecretKeyTypeCatalog SecretKeyTypeCatalog { get; } = secretKeyTypeCatalog;

    private ISecretVerifierSchemeCatalog SecretVerifierSchemeCatalog { get; } =
        secretVerifierSchemeCatalog;

    private IRemoteKeyProviderCatalog RemoteKeyProviderCatalog { get; } = remoteKeyProviderCatalog;

    /// <inheritdoc />
    public IReadOnlyCollection<SecretKey> DeserializeSecrets(
        ISecretProtector protector,
        IReadOnlyCollection<PersistedSecret> persistedSecrets
    )
    {
        ArgumentNullException.ThrowIfNull(protector);

        return DeserializeSecretsCore(protector, persistedSecrets);
    }

    /// <inheritdoc />
    public SecretKey DeserializeSecret(ISecretProtector protector, PersistedSecret persistedSecret)
    {
        ArgumentNullException.ThrowIfNull(protector);

        return DeserializeSecretCore(protector, persistedSecret, readStoredPublicKey: false);
    }

    /// <inheritdoc />
    // See ADR-1268.
    public SecretKey DeserializePublicSecret(PersistedSecret persistedSecret)
    {
        ArgumentNullException.ThrowIfNull(persistedSecret);

        if (SecretEncodings.IsPublic(persistedSecret.SecretEncoding))
        {
            return DeserializeSecretCore(null, persistedSecret, readStoredPublicKey: false);
        }

        if (persistedSecret.PublicKey is null)
        {
            throw new InvalidOperationException(
                $"The secret '{persistedSecret.SecretId}' holds '{persistedSecret.SecretEncoding}' key material and no stored public key."
            );
        }

        return DeserializeSecretCore(null, persistedSecret, readStoredPublicKey: true);
    }

    /// <inheritdoc />
    // See ADR-1391.
    public IReadOnlyCollection<SecretVerifier> DeserializeVerifiers(
        ISecretProtector protector,
        IReadOnlyCollection<PersistedSecret> persistedSecrets
    )
    {
        ArgumentNullException.ThrowIfNull(protector);
        ArgumentNullException.ThrowIfNull(persistedSecrets);

        if (persistedSecrets.Count == 0)
        {
            return [];
        }

        var verifiers = new List<SecretVerifier>(persistedSecrets.Count);
        foreach (var persistedSecret in persistedSecrets)
        {
            verifiers.Add(DeserializeVerifierCore(protector, persistedSecret));
        }

        return verifiers;
    }

    private SecretVerifier DeserializeVerifierCore(
        ISecretProtector protector,
        PersistedSecret persistedSecret
    )
    {
        if (!SecretEncodings.IsVerifier(persistedSecret.SecretEncoding))
        {
            throw new InvalidOperationException(
                $"The secret '{persistedSecret.SecretId}' holds '{persistedSecret.SecretEncoding}' key material, not a verifier."
            );
        }

        var metadata = new KeyMetadata
        {
            KeyId = persistedSecret.KeyId ?? persistedSecret.SecretId,
            Use = persistedSecret.Use,
            Algorithm = persistedSecret.Algorithm,
            ActivatesWhen = persistedSecret.ActivatesWhen,
            RetiresWhen = persistedSecret.RetiresWhen,
            ExpiresWhen = persistedSecret.ExpiresWhen,
        };

        // A verifier is protected like key material, so a database backup alone yields not even the verifier.
        using var verifierBuffer = BufferFactory.CreatePooledBufferWriter(isSensitive: true, 128);
        IBufferWriter<byte> verifierWriter = verifierBuffer;
        ReadPrivateKeyMaterial(protector, persistedSecret, verifierWriter);

        using var verifierSpanLease = verifierBuffer.GetSpanLease(isSensitive: true);
        if (
            !SecretVerifierFormat.TryParse(
                SecretVerifierSchemeCatalog,
                metadata,
                verifierSpanLease.Span,
                out var verifier
            )
        )
        {
            throw new FormatException(
                $"The secret '{persistedSecret.SecretId}' holds a verifier of a scheme that is not registered or with invalid parameters."
            );
        }

        return verifier;
    }

    private SortedSet<SecretKey> DeserializeSecretsCore(
        ISecretProtector? protector,
        IEnumerable<PersistedSecret> persistedSecrets
    )
    {
        var secretKeys = new SortedSet<SecretKey>(SecretKeyExpiresWhenComparer.Singleton);

        foreach (var persistedSecret in persistedSecrets)
        {
            secretKeys.Add(
                DeserializeSecretCore(protector, persistedSecret, readStoredPublicKey: false)
            );
        }

        return secretKeys;
    }

    private SecretKey DeserializeSecretCore(
        ISecretProtector? protector,
        PersistedSecret persistedSecret,
        bool readStoredPublicKey
    )
    {
        if (SecretEncodings.IsVerifier(persistedSecret.SecretEncoding))
        {
            // See ADR-1391.
            throw new InvalidOperationException(
                $"The secret '{persistedSecret.SecretId}' holds a verifier, which is not key material."
            );
        }

        if (!SecretKeyTypeCatalog.TryGet(persistedSecret.SecretType, out var descriptor))
        {
            throw new InvalidOperationException(
                $"The '{persistedSecret.SecretType}' secret type is not supported."
            );
        }

        var metadata = new KeyMetadata
        {
            KeyId = persistedSecret.KeyId ?? persistedSecret.SecretId,
            Use = persistedSecret.Use,
            Algorithm = persistedSecret.Algorithm,
            ActivatesWhen = persistedSecret.ActivatesWhen,
            RetiresWhen = persistedSecret.RetiresWhen,
            ExpiresWhen = persistedSecret.ExpiresWhen,
        };

        if (SecretEncodings.IsRemote(persistedSecret.SecretEncoding) && !readStoredPublicKey)
        {
            return DeserializeRemoteSecret(persistedSecret, descriptor, metadata);
        }

        const bool keyMaterialIsSensitive = true;

        var keySizeBytes = persistedSecret.KeySizeBits >> 3;
        using var keyMaterialBuffer = BufferFactory.CreatePooledBufferWriter(
            keyMaterialIsSensitive,
            keySizeBytes
        );
        IBufferWriter<byte> keyMaterialWriter = keyMaterialBuffer;

        if (readStoredPublicKey)
        {
            var storedPublicKey =
                persistedSecret.PublicKey
                ?? throw new InvalidOperationException(
                    $"The secret '{persistedSecret.SecretId}' has no stored public key."
                );
            Base64Url.Decode(storedPublicKey, keyMaterialWriter);
        }
        else if (SecretEncodings.IsPublic(persistedSecret.SecretEncoding))
        {
            ReadPublicKeyMaterial(persistedSecret, keyMaterialWriter);
        }
        else
        {
            ReadPrivateKeyMaterial(protector, persistedSecret, keyMaterialWriter);
        }

        using var keyMaterialSpanLease = keyMaterialBuffer.GetSpanLease(keyMaterialIsSensitive);

        var secretKey = descriptor.DeserializeKeyMaterial(
            metadata,
            readStoredPublicKey ? SecretEncodings.Spki : persistedSecret.SecretEncoding,
            keyMaterialSpanLease.Span
        );

        Debug.Assert(secretKey.KeySizeBits == persistedSecret.KeySizeBits);

        return secretKey;
    }

    // A remote secret holds no key material: its encoded value is the reference to the key that a key management
    // service holds, and its stored public key is the only key material read. The secret is read fail-closed, so a
    // missing provider, public key or mapping row never yields a key that cannot sign.
    // See ADR-1471.
    private SecretKey DeserializeRemoteSecret(
        PersistedSecret persistedSecret,
        ISecretKeyTypeDescriptor descriptor,
        KeyMetadata metadata
    )
    {
        var secretId = persistedSecret.SecretId;

        if (!RemoteKeyReference.TryParse(persistedSecret.EncodedValue, out var reference))
        {
            throw new FormatException(
                $"The secret '{secretId}' holds a malformed remote key reference."
            );
        }

        if (!RemoteKeyProviderCatalog.TryGetProvider(reference.Provider, out var provider))
        {
            throw new InvalidOperationException(
                $"The secret '{secretId}' names the remote key provider '{reference.Provider}', which is not registered."
            );
        }

        var storedPublicKey =
            persistedSecret.PublicKey
            ?? throw new InvalidOperationException(
                $"The secret '{secretId}' holds a remote key and has no stored public key."
            );

        var algorithmName =
            persistedSecret.Algorithm
            ?? throw new InvalidOperationException(
                $"The secret '{secretId}' holds a remote key and has no algorithm."
            );

        if (descriptor is not IRemoteSecretKeyTypeDescriptor remoteDescriptor)
        {
            throw new InvalidOperationException(
                $"The '{persistedSecret.SecretType}' secret type of the secret '{secretId}' does not support remote keys."
            );
        }

        using var publicKeyBuffer = BufferFactory.CreatePooledBufferWriter(
            isSensitive: false,
            persistedSecret.KeySizeBits >> 3
        );
        IBufferWriter<byte> publicKeyWriter = publicKeyBuffer;
        Base64Url.Decode(storedPublicKey, publicKeyWriter);

        using var publicKeySpanLease = publicKeyBuffer.GetSpanLease(isSensitive: false);

        // The stored public key decides the size and curve that the mapping row must match.
        RemoteSigningAlgorithmSelector selector;
        var storedKey = descriptor.DeserializeKeyMaterial(
            metadata,
            SecretEncodings.Spki,
            publicKeySpanLease.Span
        );
        try
        {
            selector = new RemoteSigningAlgorithmSelector
            {
                Provider = reference.Provider,
                Algorithm = algorithmName,
                SecretType = persistedSecret.SecretType,
                KeySizeBits = storedKey.KeySizeBits,
                Curve = (storedKey as AsymmetricSecretKey)?.CurveName,
            };
        }
        finally
        {
            if (storedKey is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        if (!RemoteKeyProviderCatalog.TryGetAlgorithm(selector, out var algorithm))
        {
            throw new InvalidOperationException(
                $"The secret '{secretId}' has no remote signing algorithm for provider '{selector.Provider}', "
                    + $"algorithm '{selector.Algorithm}', secret type '{selector.SecretType}', key size {selector.KeySizeBits} and curve '{selector.Curve}'."
            );
        }

        var binding = new RemoteKeyBinding
        {
            Provider = provider,
            Reference = reference,
            Algorithm = algorithm,
        };

        try
        {
            return remoteDescriptor.CreateRemoteKey(metadata, publicKeySpanLease.Span, binding);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(
                $"The secret '{secretId}' holds a remote key that does not match its remote signing algorithm.",
                exception
            );
        }
    }

    // Public key material is stored as plain base64url text.
    private static void ReadPublicKeyMaterial(
        PersistedSecret persistedSecret,
        IBufferWriter<byte> destination
    ) => Base64Url.Decode(persistedSecret.EncodedValue, destination);

    // Private and symmetric key material is stored as an envelope that is protected by the master key of its owner.
    private static void ReadPrivateKeyMaterial(
        ISecretProtector? protector,
        PersistedSecret persistedSecret,
        IBufferWriter<byte> destination
    )
    {
        var encodedValue = persistedSecret.EncodedValue;

        if (!encodedValue.StartsWith(SecretProtectionFormat.Prefix, StringComparison.Ordinal))
        {
            throw new CryptographicException(
                $"The secret '{persistedSecret.SecretId}' is not a protected envelope."
            );
        }

        if (protector is null)
        {
            throw new CryptographicException(
                $"The secret '{persistedSecret.SecretId}' is protected under a master key and needs a protector to be read."
            );
        }

        protector.Unprotect(
            encodedValue,
            SecretProtectionContext.ForPersistedSecret(persistedSecret.SecretId),
            ref destination
        );
    }
}
