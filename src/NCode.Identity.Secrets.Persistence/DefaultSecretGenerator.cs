#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

using System.Security.Cryptography;
using NCode.Buffers;
using NCode.Encoders;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.Secrets.Persistence;

/// <summary>
/// Provides a default implementation of the <see cref="ISecretGenerator"/> abstraction that generates key
/// material server-side and protects it under the owner's master key through an <see cref="ISecretProtector"/>. The
/// per-type generation logic is delegated to the registered <see cref="ISecretKeyTypeDescriptor"/> for the requested
/// key type.
/// </summary>
// See ADR-0067.
internal class DefaultSecretGenerator(ISecretKeyTypeCatalog secretKeyTypeCatalog) : ISecretGenerator
{
    private ISecretKeyTypeCatalog SecretKeyTypeCatalog { get; } = secretKeyTypeCatalog;

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
        };

        return new GeneratedSecret { Secret = secret, SecretMaterial = material.SecretMaterial };
    }

    /// <inheritdoc />
    public GeneratedSecret GenerateSharedSecret(
        ISecretProtector protector,
        GenerateSecretRequest request
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

        // The opaque secret string is what an external party holds and later presents; its UTF-8 bytes are stored as
        // the symmetric key material so a UTF-8 comparison of the presented secret against the stored material matches.
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

        var secret = CreateSharedSecret(
            request.SecretId,
            request.Use,
            request.Algorithm,
            request.CreatedWhen,
            request.ExpiresWhen,
            utf8,
            bytes =>
                protector.ProtectToString(
                    bytes,
                    SecretProtectionContext.ForPersistedSecret(request.SecretId)
                )
        );

        return new GeneratedSecret { Secret = secret, SecretMaterial = secretMaterial };
    }

    /// <inheritdoc />
    public PersistedSecret ImportSharedSecret(
        ISecretProtector protector,
        ImportSharedSecretRequest request,
        ReadOnlySpan<byte> secretValue
    )
    {
        if (secretValue.IsEmpty)
        {
            throw new ArgumentException(
                "The shared-secret value must not be empty.",
                nameof(secretValue)
            );
        }

        return CreateSharedSecret(
            request.SecretId,
            request.Use,
            request.Algorithm,
            request.CreatedWhen,
            request.ExpiresWhen,
            secretValue,
            bytes =>
                protector.ProtectToString(
                    bytes,
                    SecretProtectionContext.ForPersistedSecret(request.SecretId)
                )
        );
    }

    // The single correct-by-default construction of a shared-secret PersistedSecret (ADR-0110): a symmetric_raw secret
    // whose stored key material is exactly the supplied UTF-8 octets of the opaque string, shared by GenerateSharedSecret
    // (random value) and ImportSharedSecret (caller-supplied value) so the stored shape cannot drift between them.
    private static PersistedSecret CreateSharedSecret(
        string secretId,
        string? use,
        string? algorithm,
        DateTimeOffset createdWhen,
        DateTimeOffset expiresWhen,
        ReadOnlySpan<byte> utf8SecretBytes,
        SecretKeyMaterialProtector protect
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
            SecretEncoding = SecretEncodings.SymmetricRaw,
            KeySizeBits = utf8SecretBytes.Length * 8,
            EncodedValue = protect(utf8SecretBytes),
        };
    }
}
