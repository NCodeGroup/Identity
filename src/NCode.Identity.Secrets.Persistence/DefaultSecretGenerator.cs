// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using NCode.Buffers;
using NCode.Encoders;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Secrets.Persistence.Verifiers;
using NCode.Identity.Secrets.RemoteKeys;
using NCode.Identity.Secrets.Verifiers;

namespace NCode.Identity.Secrets.Persistence;

/// <summary>
/// Provides a default implementation of the <see cref="ISecretGenerator"/> abstraction that generates key
/// material server-side and protects it under the owner's master key through an <see cref="ISecretProtector"/>. The
/// per-type generation logic is delegated to the registered <see cref="ISecretKeyTypeDescriptor"/> for the requested
/// key type. A shared secret that is only ever compared is stored as its one-way verifier, made by the registered
/// <see cref="ISecretVerifierScheme"/> that <see cref="SharedSecretVerifierOptions"/> names for a generated secret or
/// for a supplied one. A remote key is created by its registered <see cref="IRemoteKeyProvider"/>, which keeps the
/// private key in a key management service, and only its public half and a reference to it are stored.
/// </summary>
// See ADR-1067, ADR-1110, ADR-1391 and ADR-1471.
internal class DefaultSecretGenerator(
    ISecretKeyTypeCatalog secretKeyTypeCatalog,
    ISecretVerifierSchemeCatalog secretVerifierSchemeCatalog,
    IOptions<SharedSecretVerifierOptions> verifierOptionsAccessor,
    IRemoteKeyProviderCatalog remoteKeyProviderCatalog
) : ISecretGenerator
{
    private ISecretKeyTypeCatalog SecretKeyTypeCatalog { get; } = secretKeyTypeCatalog;

    private IRemoteKeyProviderCatalog RemoteKeyProviderCatalog { get; } = remoteKeyProviderCatalog;

    private ISecretVerifierScheme GeneratedSecretScheme { get; } =
        GetScheme(
            secretVerifierSchemeCatalog,
            nameof(SharedSecretVerifierOptions.GeneratedSecretScheme),
            verifierOptionsAccessor.Value.GeneratedSecretScheme
        );

    private ISecretVerifierScheme SuppliedSecretScheme { get; } =
        GetScheme(
            secretVerifierSchemeCatalog,
            nameof(SharedSecretVerifierOptions.SuppliedSecretScheme),
            verifierOptionsAccessor.Value.SuppliedSecretScheme
        );

    private static ISecretVerifierScheme GetScheme(
        ISecretVerifierSchemeCatalog catalog,
        string optionName,
        string schemeName
    ) =>
        catalog.TryGet(schemeName, out var scheme)
            ? scheme
            : throw new InvalidOperationException(
                $"'{nameof(SharedSecretVerifierOptions)}.{optionName}' is '{schemeName}', which names no registered secret verifier scheme."
            );

    /// <inheritdoc />
    public GeneratedSecret GenerateSecret(ISecretProtector protector, GenerateSecretRequest request)
    {
        if (!SecretKeyTypeCatalog.TryGet(request.SecretType, out var descriptor))
        {
            throw new InvalidOperationException(
                $"The '{request.SecretType}' secret type cannot be generated."
            );
        }

        // The lifecycle is checked before any material is generated, so an out-of-order request costs no key pair.
        var (activatesWhen, retiresWhen) = SecretLifecycle.Resolve(
            request.CreatedWhen,
            request.ActivatesWhen,
            request.RetiresWhen,
            request.ExpiresWhen
        );

        var material = descriptor.GenerateKeyMaterial(
            request.GenerationParameters,
            bytes =>
                protector.ProtectToString(
                    bytes,
                    SecretProtectionContext.ForPersistedSecret(request.SecretId)
                )
        );

        var secret = new PersistedSecret
        {
            SecretId = request.SecretId,
            Use = request.Use,
            Algorithm = request.Algorithm,
            CreatedWhen = request.CreatedWhen,
            ActivatesWhen = activatesWhen,
            RetiresWhen = retiresWhen,
            ExpiresWhen = request.ExpiresWhen,
            SecretType = request.SecretType,
            SecretEncoding = material.SecretEncoding,
            KeySizeBits = material.KeySizeBits,
            EncodedValue = material.ProtectedKeyMaterial,
            PublicKey = material.PublicKey,
            Origin = SecretOrigins.Generated,
            ManagedBy = request.ManagedBy,
        };

        return new GeneratedSecret { Secret = secret, SecretMaterial = material.SecretMaterial };
    }

    /// <inheritdoc />
    // See ADR-1471.
    public async ValueTask<PersistedSecret> GenerateRemoteSecretAsync(
        GenerateRemoteSecretRequest request,
        CancellationToken cancellationToken
    )
    {
        // Every check that can refuse the request runs before the key management service is called, so a refused
        // request creates no key.
        if (
            !SecretKeyTypeCatalog.TryGet(request.SecretType, out var descriptor)
            || descriptor is not IRemoteSecretKeyTypeDescriptor
        )
        {
            throw new InvalidOperationException(
                $"The '{request.SecretType}' secret type cannot be generated in a key management service."
            );
        }

        var (activatesWhen, retiresWhen) = SecretLifecycle.Resolve(
            request.CreatedWhen,
            request.ActivatesWhen,
            request.RetiresWhen,
            request.ExpiresWhen
        );

        if (!RemoteKeyProviderCatalog.TryGetProvider(request.Provider, out var provider))
        {
            throw new InvalidOperationException(
                $"The remote key provider '{request.Provider}' is not registered."
            );
        }

        var selector = new RemoteSigningAlgorithmSelector
        {
            Provider = request.Provider,
            Algorithm = request.Algorithm,
            SecretType = request.SecretType,
            KeySizeBits = request.KeySizeBits,
            Curve = request.Curve,
        };

        if (!RemoteKeyProviderCatalog.TryGetAlgorithm(selector, out var algorithm))
        {
            throw new InvalidOperationException(
                $"The remote key provider '{request.Provider}' has no signing algorithm '{request.Algorithm}' "
                    + $"for the '{request.SecretType}' secret type, key size {request.KeySizeBits} and curve '{request.Curve}'."
            );
        }

        var created = await provider.CreateKeyAsync(
            new RemoteKeyCreationRequest
            {
                OwnerId = request.Owner.ToString(),
                SecretId = request.SecretId,
                Algorithm = algorithm,
            },
            cancellationToken
        );

        var metadata = new KeyMetadata
        {
            KeyId = request.SecretId,
            Use = request.Use,
            Algorithm = request.Algorithm,
            ActivatesWhen = activatesWhen,
            RetiresWhen = retiresWhen,
            ExpiresWhen = request.ExpiresWhen,
        };

        if (!HoldsRequestedKey(descriptor, metadata, created.SubjectPublicKeyInfo, algorithm))
        {
            try
            {
                await provider.DestroyKeyAsync(created.KeyId, cancellationToken);
            }
            catch (Exception)
            {
                // The mismatch is the error reported, so a failure to destroy the key does not replace it.
            }

            throw new InvalidOperationException(
                $"The remote key provider '{request.Provider}' created a key that does not have the key size "
                    + $"{algorithm.KeySizeBits} and curve '{algorithm.Curve}' of its signing algorithm '{algorithm.Algorithm}'."
            );
        }

        return new PersistedSecret
        {
            SecretId = request.SecretId,
            Use = request.Use,
            Algorithm = request.Algorithm,
            CreatedWhen = request.CreatedWhen,
            ActivatesWhen = activatesWhen,
            RetiresWhen = retiresWhen,
            ExpiresWhen = request.ExpiresWhen,
            SecretType = request.SecretType,
            SecretEncoding = SecretEncodings.Remote,
            KeySizeBits = algorithm.KeySizeBits,
            EncodedValue = new RemoteKeyReference(provider.Name, created.KeyId).ToString(),
            PublicKey = Base64Url.Encode(created.SubjectPublicKeyInfo.Span),
            Origin = SecretOrigins.Generated,
            ManagedBy = request.ManagedBy,
        };
    }

    // Reads the public key the service returned, which decides whether the key it created has the size and curve of
    // the signing algorithm's row.
    private static bool HoldsRequestedKey(
        ISecretKeyTypeDescriptor descriptor,
        KeyMetadata metadata,
        ReadOnlyMemory<byte> subjectPublicKeyInfo,
        RemoteSigningAlgorithm algorithm
    )
    {
        var createdKey = descriptor.DeserializeKeyMaterial(
            metadata,
            SecretEncodings.Spki,
            subjectPublicKeyInfo.Span
        );
        try
        {
            return createdKey.KeySizeBits == algorithm.KeySizeBits
                && string.Equals(
                    (createdKey as AsymmetricSecretKey)?.CurveName,
                    algorithm.Curve,
                    StringComparison.Ordinal
                );
        }
        finally
        {
            if (createdKey is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    /// <inheritdoc />
    public GeneratedSecret GenerateSharedSecretVerifier(
        ISecretProtector protector,
        SharedSecretRequest request
    )
    {
        var secretMaterial = GenerateSecretMaterial(request);
        using var utf8Lease = RentUtf8(secretMaterial, out var utf8);

        // A generated secret has at least 256 bits of entropy, which no search can guess, so its scheme needs no work
        // factor.
        var secret = CreateVerifierSecret(
            protector,
            GeneratedSecretScheme,
            request,
            SecretOrigins.Generated,
            utf8
        );

        return new GeneratedSecret { Secret = secret, SecretMaterial = secretMaterial };
    }

    /// <inheritdoc />
    public GeneratedSecret GenerateReversibleSharedSecret(
        ISecretProtector protector,
        SharedSecretRequest request
    )
    {
        var secretMaterial = GenerateSecretMaterial(request);
        using var utf8Lease = RentUtf8(secretMaterial, out var utf8);

        var secret = CreateReversibleSecret(protector, request, SecretOrigins.Generated, utf8);

        return new GeneratedSecret { Secret = secret, SecretMaterial = secretMaterial };
    }

    /// <inheritdoc />
    public PersistedSecret ImportSharedSecretVerifier(
        ISecretProtector protector,
        SharedSecretRequest request,
        ReadOnlySpan<byte> secretValue
    )
    {
        EnsureNotEmpty(secretValue);

        // A supplied secret was chosen by someone and its entropy is unknown, so its scheme carries the work factor of a
        // password hash.
        return CreateVerifierSecret(
            protector,
            SuppliedSecretScheme,
            request,
            SecretOrigins.Supplied,
            secretValue
        );
    }

    /// <inheritdoc />
    public PersistedSecret ImportReversibleSharedSecret(
        ISecretProtector protector,
        SharedSecretRequest request,
        ReadOnlySpan<byte> secretValue
    )
    {
        EnsureNotEmpty(secretValue);

        return CreateReversibleSecret(protector, request, SecretOrigins.Supplied, secretValue);
    }

    /// <inheritdoc />
    public PersistedSecret ConvertToSharedSecretVerifier(
        ISecretProtector protector,
        PersistedSecret secret,
        string secretId
    )
    {
        if (
            !string.Equals(secret.SecretType, SecretTypes.Symmetric, StringComparison.Ordinal)
            || !string.Equals(
                secret.SecretEncoding,
                SecretEncodings.SymmetricRaw,
                StringComparison.Ordinal
            )
        )
        {
            throw new InvalidOperationException(
                $"The secret '{secret.SecretId}' is not a shared secret stored as its octets."
            );
        }

        using var octetsBuffer = BufferFactory.CreatePooledBufferWriter(isSensitive: true, 64);
        IBufferWriter<byte> octetsWriter = octetsBuffer;
        protector.Unprotect(
            secret.EncodedValue,
            SecretProtectionContext.ForPersistedSecret(secret.SecretId),
            ref octetsWriter
        );
        using var octetsLease = octetsBuffer.GetSpanLease(isSensitive: true);

        // Only a secret the server generated is known to have the entropy that lets its verifier skip the work factor.
        var isGenerated = string.Equals(
            secret.Origin,
            SecretOrigins.Generated,
            StringComparison.Ordinal
        );

        return CreateVerifierSecret(
            protector,
            isGenerated ? GeneratedSecretScheme : SuppliedSecretScheme,
            new SharedSecretRequest
            {
                SecretId = secretId,
                Use = secret.Use,
                Algorithm = secret.Algorithm,
                CreatedWhen = secret.CreatedWhen,
                ExpiresWhen = secret.ExpiresWhen,
            },
            secret.Origin,
            octetsLease.Span
        );
    }

    // Generates the opaque, URL-safe secret string an external party holds and later presents: the one-time reveal
    // returned to the caller, a wire-boundary string.
    private static string GenerateSecretMaterial(SharedSecretRequest request)
    {
        if (request.EntropyBits <= 0 || request.EntropyBits % 8 != 0)
        {
            throw new ArgumentException(
                "The shared-secret entropy size (in bits) must be a positive multiple of 8.",
                nameof(request)
            );
        }

        if (request.EntropyBits < SharedSecretPolicy.MinimumEntropyBits)
        {
            throw new ArgumentException(
                $"A shared secret must have at least {SharedSecretPolicy.MinimumEntropyBits} bits of entropy.",
                nameof(request)
            );
        }

        var entropyByteLength = request.EntropyBits >> 3;
        using var entropyLease = BufferFactory.Rent(
            entropyByteLength,
            isSensitive: true,
            out Span<byte> entropy
        );
        RandomNumberGenerator.Fill(entropy);

        return Base64Url.Encode(entropy);
    }

    // What is stored is computed from the secret's UTF-8 octets, which is what a presented secret is compared as.
    private static IDisposable RentUtf8(string secretMaterial, out Span<byte> utf8)
    {
        var lease = BufferFactory.Rent(
            SecureEncoding.UTF8.GetByteCount(secretMaterial),
            isSensitive: true,
            out utf8
        );
        SecureEncoding.UTF8.GetBytes(secretMaterial, utf8);
        return lease;
    }

    // Stores the scheme's one-way verifier of the secret, protected like key material.
    private static PersistedSecret CreateVerifierSecret(
        ISecretProtector protector,
        ISecretVerifierScheme scheme,
        SharedSecretRequest request,
        string origin,
        ReadOnlySpan<byte> secret
    )
    {
        using var verifierBuffer = BufferFactory.CreatePooledBufferWriter(isSensitive: true, 128);
        SecretVerifierFormat.Write(scheme, secret, verifierBuffer);

        using var verifierSpanLease = verifierBuffer.GetSpanLease(isSensitive: true);
        var encodedValue = Protect(protector, request.SecretId, verifierSpanLease.Span);

        return CreateSharedSecret(
            request,
            SecretEncodings.Verifier,
            origin,
            secret.Length,
            encodedValue
        );
    }

    // Stores the secret's UTF-8 octets themselves, protected, so a method that keys from the secret can read it back.
    private static PersistedSecret CreateReversibleSecret(
        ISecretProtector protector,
        SharedSecretRequest request,
        string origin,
        ReadOnlySpan<byte> secret
    ) =>
        CreateSharedSecret(
            request,
            SecretEncodings.SymmetricRaw,
            origin,
            secret.Length,
            Protect(protector, request.SecretId, secret)
        );

    private static void EnsureNotEmpty(ReadOnlySpan<byte> secretValue)
    {
        if (secretValue.IsEmpty)
        {
            throw new ArgumentException(
                "The shared-secret value must not be empty.",
                nameof(secretValue)
            );
        }
    }

    private static string Protect(
        ISecretProtector protector,
        string secretId,
        ReadOnlySpan<byte> bytes
    ) => protector.ProtectToString(bytes, SecretProtectionContext.ForPersistedSecret(secretId));

    // The single construction of a shared-secret PersistedSecret, shared by every generate, import and convert method so
    // the stored shape cannot drift between them: a Symmetric secret whose size is that of the secret's UTF-8 octets and
    // whose stored value is the protected verifier or the protected octets themselves. See ADR-1110 and ADR-1391.
    private static PersistedSecret CreateSharedSecret(
        SharedSecretRequest request,
        string secretEncoding,
        string origin,
        int utf8SecretByteCount,
        string encodedValue
    ) =>
        new()
        {
            SecretId = request.SecretId,
            Use = request.Use,
            Algorithm = request.Algorithm,
            // A shared secret is a credential its holder presents, with no lifecycle of its own beyond its validity:
            // it authenticates from its creation to its expiry.
            CreatedWhen = request.CreatedWhen,
            ActivatesWhen = request.CreatedWhen,
            RetiresWhen = request.ExpiresWhen,
            ExpiresWhen = request.ExpiresWhen,
            SecretType = SecretTypes.Symmetric,
            SecretEncoding = secretEncoding,
            Origin = origin,
            KeySizeBits = utf8SecretByteCount * 8,
            EncodedValue = encodedValue,
        };
}
