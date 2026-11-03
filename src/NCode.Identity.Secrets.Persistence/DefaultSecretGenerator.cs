// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using NCode.Buffers;
using NCode.Encoders;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Secrets.Persistence.Verifiers;
using NCode.Identity.Secrets.Verifiers;

namespace NCode.Identity.Secrets.Persistence;

/// <summary>
/// Provides a default implementation of the <see cref="ISecretGenerator"/> abstraction that generates key
/// material server-side and protects it under the owner's master key through an <see cref="ISecretProtector"/>. The
/// per-type generation logic is delegated to the registered <see cref="ISecretKeyTypeDescriptor"/> for the requested
/// key type. A shared secret that is only ever compared is stored as its one-way verifier, made by the registered
/// <see cref="ISecretVerifierScheme"/> that <see cref="SharedSecretVerifierOptions"/> names for a generated secret or
/// for a supplied one.
/// </summary>
// See ADR-1067, ADR-1110 and ADR-1391.
internal class DefaultSecretGenerator(
    ISecretKeyTypeCatalog secretKeyTypeCatalog,
    ISecretVerifierSchemeCatalog secretVerifierSchemeCatalog,
    IOptions<SharedSecretVerifierOptions> verifierOptionsAccessor
) : ISecretGenerator
{
    private ISecretKeyTypeCatalog SecretKeyTypeCatalog { get; } = secretKeyTypeCatalog;

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

        var material = descriptor.GenerateKeyMaterial(
            new SecretKeyGenerationParameters
            {
                KeySizeBits = request.KeySizeBits,
                Curve = request.Curve,
            },
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
            ExpiresWhen = request.ExpiresWhen,
            SecretType = request.SecretType,
            SecretEncoding = material.SecretEncoding,
            KeySizeBits = material.KeySizeBits,
            EncodedValue = material.ProtectedKeyMaterial,
            PublicKey = material.PublicKey,
        };

        return new GeneratedSecret { Secret = secret, SecretMaterial = material.SecretMaterial };
    }

    /// <inheritdoc />
    public GeneratedSecret GenerateOneWaySharedSecret(
        ISecretProtector protector,
        GenerateSecretRequest request
    )
    {
        var secretMaterial = GenerateSecretMaterial(request);
        using var utf8Lease = RentUtf8(secretMaterial, out var utf8);

        // A generated secret has at least 256 bits of entropy, which no search can guess, so its scheme needs no work
        // factor.
        var secret = CreateVerifierSecret(
            protector,
            GeneratedSecretScheme,
            SharedSecretMetadata.From(request),
            utf8
        );

        return new GeneratedSecret { Secret = secret, SecretMaterial = secretMaterial };
    }

    /// <inheritdoc />
    public GeneratedSecret GenerateReversibleSharedSecret(
        ISecretProtector protector,
        GenerateSecretRequest request
    )
    {
        var secretMaterial = GenerateSecretMaterial(request);
        using var utf8Lease = RentUtf8(secretMaterial, out var utf8);

        var secret = CreateReversibleSecret(protector, SharedSecretMetadata.From(request), utf8);

        return new GeneratedSecret { Secret = secret, SecretMaterial = secretMaterial };
    }

    /// <inheritdoc />
    public PersistedSecret ImportOneWaySharedSecret(
        ISecretProtector protector,
        ImportSharedSecretRequest request,
        ReadOnlySpan<byte> secretValue
    )
    {
        EnsureNotEmpty(secretValue);

        // A supplied secret was chosen by someone and its entropy is unknown, so its scheme carries the work factor of a
        // password hash.
        return CreateVerifierSecret(
            protector,
            SuppliedSecretScheme,
            SharedSecretMetadata.From(request),
            secretValue
        );
    }

    /// <inheritdoc />
    public PersistedSecret ImportReversibleSharedSecret(
        ISecretProtector protector,
        ImportSharedSecretRequest request,
        ReadOnlySpan<byte> secretValue
    )
    {
        EnsureNotEmpty(secretValue);

        return CreateReversibleSecret(protector, SharedSecretMetadata.From(request), secretValue);
    }

    // Generates the opaque, URL-safe secret string an external party holds and later presents: the one-time reveal
    // returned to the caller, a wire-boundary string.
    private static string GenerateSecretMaterial(GenerateSecretRequest request)
    {
        if (request.SecretType != SecretTypes.Symmetric)
        {
            throw new InvalidOperationException(
                $"A shared secret must be of type '{SecretTypes.Symmetric}'."
            );
        }

        if (request.KeySizeBits <= 0 || request.KeySizeBits % 8 != 0)
        {
            throw new ArgumentException(
                "The shared-secret entropy size (in bits) must be a positive multiple of 8.",
                nameof(request)
            );
        }

        if (request.KeySizeBits < SharedSecretPolicy.MinimumEntropyBits)
        {
            throw new ArgumentException(
                $"A shared secret must have at least {SharedSecretPolicy.MinimumEntropyBits} bits of entropy.",
                nameof(request)
            );
        }

        var entropyByteLength = request.KeySizeBits >> 3;
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
        SharedSecretMetadata metadata,
        ReadOnlySpan<byte> secret
    )
    {
        using var verifierBuffer = BufferFactory.CreatePooledBufferWriter(isSensitive: true, 128);
        SecretVerifierFormat.Write(scheme, secret, verifierBuffer);

        using var verifierSpanLease = verifierBuffer.GetSpanLease(isSensitive: true);
        var encodedValue = Protect(protector, metadata.SecretId, verifierSpanLease.Span);

        return CreateSharedSecret(metadata, SecretEncodings.Verifier, secret.Length, encodedValue);
    }

    // Stores the secret's UTF-8 octets themselves, protected, so a method that keys from the secret can read it back.
    private static PersistedSecret CreateReversibleSecret(
        ISecretProtector protector,
        SharedSecretMetadata metadata,
        ReadOnlySpan<byte> secret
    ) =>
        CreateSharedSecret(
            metadata,
            SecretEncodings.SymmetricRaw,
            secret.Length,
            Protect(protector, metadata.SecretId, secret)
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

    // The single construction of a shared-secret PersistedSecret, shared by every generate and import method so the
    // stored shape cannot drift between them: a Symmetric secret whose size is that of the secret's UTF-8 octets and
    // whose stored value is the protected verifier or the protected octets themselves. See ADR-1110 and ADR-1391.
    private static PersistedSecret CreateSharedSecret(
        SharedSecretMetadata metadata,
        string secretEncoding,
        int utf8SecretByteCount,
        string encodedValue
    ) =>
        new()
        {
            SecretId = metadata.SecretId,
            Use = metadata.Use,
            Algorithm = metadata.Algorithm,
            CreatedWhen = metadata.CreatedWhen,
            ExpiresWhen = metadata.ExpiresWhen,
            SecretType = SecretTypes.Symmetric,
            SecretEncoding = secretEncoding,
            KeySizeBits = utf8SecretByteCount * 8,
            EncodedValue = encodedValue,
        };
}
