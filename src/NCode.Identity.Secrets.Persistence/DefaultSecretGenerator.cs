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
    public GeneratedSecret GenerateSharedSecret(
        ISecretProtector protector,
        GenerateSecretRequest request
    )
    {
        // A generated secret has at least 256 bits of entropy, which no search can guess, so its scheme needs no work
        // factor.
        var scheme = GeneratedSecretScheme;
        return GenerateSharedSecretCore(
            protector,
            request,
            (utf8, protect) => (SecretEncodings.Verifier, ProtectVerifier(scheme, utf8, protect))
        );
    }

    /// <inheritdoc />
    public GeneratedSecret GenerateReversibleSharedSecret(
        ISecretProtector protector,
        GenerateSecretRequest request
    ) =>
        GenerateSharedSecretCore(
            protector,
            request,
            static (utf8, protect) => (SecretEncodings.SymmetricRaw, protect(utf8))
        );

    /// <inheritdoc />
    public PersistedSecret ImportSharedSecret(
        ISecretProtector protector,
        ImportSharedSecretRequest request,
        ReadOnlySpan<byte> secretValue
    )
    {
        EnsureNotEmpty(secretValue);

        // A supplied secret was chosen by someone and its entropy is unknown, so its scheme carries the work factor of a
        // password hash.
        var secretId = request.SecretId;
        var encodedValue = ProtectVerifier(
            SuppliedSecretScheme,
            secretValue,
            bytes => Protect(protector, secretId, bytes)
        );

        return CreateSharedSecret(
            secretId,
            request.Use,
            request.Algorithm,
            request.CreatedWhen,
            request.ExpiresWhen,
            SecretEncodings.Verifier,
            secretValue.Length,
            encodedValue
        );
    }

    // Writes the scheme's verifier text of the secret into a sensitive buffer and protects it.
    private static string ProtectVerifier(
        ISecretVerifierScheme scheme,
        ReadOnlySpan<byte> secret,
        SecretKeyMaterialProtector protect
    )
    {
        using var verifierBuffer = BufferFactory.CreatePooledBufferWriter(isSensitive: true, 128);
        SecretVerifierFormat.Write(scheme, secret, verifierBuffer);

        using var verifierSpanLease = verifierBuffer.GetSpanLease(isSensitive: true);
        return protect(verifierSpanLease.Span);
    }

    /// <inheritdoc />
    public PersistedSecret ImportReversibleSharedSecret(
        ISecretProtector protector,
        ImportSharedSecretRequest request,
        ReadOnlySpan<byte> secretValue
    )
    {
        EnsureNotEmpty(secretValue);

        return CreateSharedSecret(
            request.SecretId,
            request.Use,
            request.Algorithm,
            request.CreatedWhen,
            request.ExpiresWhen,
            SecretEncodings.SymmetricRaw,
            secretValue.Length,
            Protect(protector, request.SecretId, secretValue)
        );
    }

    private delegate (string SecretEncoding, string EncodedValue) SharedSecretEncoder(
        ReadOnlySpan<byte> utf8SecretBytes,
        SecretKeyMaterialProtector protect
    );

    private static GeneratedSecret GenerateSharedSecretCore(
        ISecretProtector protector,
        GenerateSecretRequest request,
        SharedSecretEncoder encode
    )
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

        // The opaque secret string is what an external party holds and later presents; what is stored is computed from
        // its UTF-8 bytes, which is what a presented secret is compared as.
        var entropyByteLength = request.KeySizeBits >> 3;
        using var entropyLease = BufferFactory.Rent(
            entropyByteLength,
            isSensitive: true,
            out Span<byte> entropy
        );
        RandomNumberGenerator.Fill(entropy);

        // The one-time reveal returned to the caller, a wire-boundary string.
        var secretMaterial = Base64Url.Encode(entropy);

        using var utf8Lease = BufferFactory.Rent(
            SecureEncoding.UTF8.GetByteCount(secretMaterial),
            isSensitive: true,
            out Span<byte> utf8
        );
        SecureEncoding.UTF8.GetBytes(secretMaterial, utf8);

        var secretId = request.SecretId;
        var (secretEncoding, encodedValue) = encode(
            utf8,
            bytes => Protect(protector, secretId, bytes)
        );

        var secret = CreateSharedSecret(
            secretId,
            request.Use,
            request.Algorithm,
            request.CreatedWhen,
            request.ExpiresWhen,
            secretEncoding,
            utf8.Length,
            encodedValue
        );

        return new GeneratedSecret { Secret = secret, SecretMaterial = secretMaterial };
    }

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
        string secretId,
        string? use,
        string? algorithm,
        DateTimeOffset createdWhen,
        DateTimeOffset expiresWhen,
        string secretEncoding,
        int utf8SecretByteCount,
        string encodedValue
    )
    {
        return new PersistedSecret
        {
            SecretId = secretId,
            Use = use,
            Algorithm = algorithm,
            CreatedWhen = createdWhen,
            ExpiresWhen = expiresWhen,
            SecretType = SecretTypes.Symmetric,
            SecretEncoding = secretEncoding,
            KeySizeBits = utf8SecretByteCount * 8,
            EncodedValue = encodedValue,
        };
    }
}
