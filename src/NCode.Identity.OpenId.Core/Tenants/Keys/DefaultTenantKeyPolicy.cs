// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Tenants.Keys;

/// <summary>
/// Provides the default implementation of <see cref="ITenantKeyPolicy"/>. A configured key names only its algorithm;
/// the key's use and type follow from the registered algorithm and key types, so any registered asymmetric signature or
/// key-management algorithm can be provisioned without this policy knowing it.
/// </summary>
// See ADR-1169 (every tenant is provisioned with persisted keys).
internal class DefaultTenantKeyPolicy(
    IAlgorithmCollectionProvider algorithmCollectionProvider,
    ISecretKeyTypeCatalog secretKeyTypeCatalog,
    ISecretSerializer secretSerializer
) : ITenantKeyPolicy
{
    // RSA keys are 3072 bits because NIST SP 800-57 accepts 2048-bit RSA only through 2030, and a provisioned key has
    // no lifetime of its own until key rollover exists (issue #166).
    private const int DefaultRsaKeySizeBits = 3072;

    private IAlgorithmCollectionProvider AlgorithmCollectionProvider { get; } =
        algorithmCollectionProvider;

    private ISecretKeyTypeCatalog SecretKeyTypeCatalog { get; } = secretKeyTypeCatalog;

    private ISecretSerializer SecretSerializer { get; } = secretSerializer;

    // RS256 is the algorithm every OpenID provider must support (OpenID Connect Core 1.0 §15.1); PS256 and ES256 are the
    // modern algorithms the FAPI 2.0 security profile permits. One key per algorithm binds each key to the single
    // algorithm it is used with (RFC 8725 §3.1).
    private static List<OpenIdTenantKeyOptions> DefaultKeys =>
        [
            new()
            {
                Algorithm = AlgorithmCodes.DigitalSignature.RsaSha256,
                KeySizeBits = DefaultRsaKeySizeBits,
            },
            new()
            {
                Algorithm = AlgorithmCodes.DigitalSignature.RsaSsaPssSha256,
                KeySizeBits = DefaultRsaKeySizeBits,
            },
            new() { Algorithm = AlgorithmCodes.DigitalSignature.EcdsaSha256 },
        ];

    /// <inheritdoc />
    public TenantKeySpecificationResolution ResolveSpecifications(
        OpenIdTenantKeyProvisioningOptions options
    )
    {
        ArgumentNullException.ThrowIfNull(options);

        var specifications = new List<TenantKeySpecification>();
        var errors = new List<string>();

        if (options.Enabled)
        {
            var keys = options.Keys.Count > 0 ? options.Keys : DefaultKeys;
            var algorithms = new HashSet<string>(StringComparer.Ordinal);
            var collection = AlgorithmCollectionProvider.Collection;

            foreach (var key in keys)
            {
                if (string.IsNullOrEmpty(key.Algorithm))
                {
                    errors.Add("A provisioned tenant key does not name an algorithm.");
                }
                else if (!algorithms.Add(key.Algorithm))
                {
                    errors.Add(
                        $"The algorithm '{key.Algorithm}' is listed more than once; a tenant is provisioned with one "
                            + "key per algorithm."
                    );
                }
                else if (
                    TryResolveSpecification(collection, key, out var specification, out var error)
                )
                {
                    specifications.Add(specification);
                }
                else
                {
                    errors.Add(error);
                }
            }
        }

        return new TenantKeySpecificationResolution
        {
            Specifications = specifications,
            Errors = errors,
        };
    }

    /// <inheritdoc />
    public bool IsSatisfiedBy(TenantKeySpecification specification, PersistedSecret secret)
    {
        ArgumentNullException.ThrowIfNull(secret);

        // A key bound to another algorithm is never selected for this one; an unbound key serves any algorithm its type
        // and size fit, as the runtime credential selector treats it.
        if (
            secret.Algorithm is not null
            && !string.Equals(secret.Algorithm, specification.Algorithm, StringComparison.Ordinal)
        )
        {
            return false;
        }

        // Public material cannot sign or decrypt. Only the encodings known to carry a public key are excluded, so a key
        // held in a form this policy does not know — an external keystore's, say — is trusted rather than duplicated.
        if (
            string.Equals(secret.SecretEncoding, SecretEncodings.Spki, StringComparison.Ordinal)
            || string.Equals(secret.SecretEncoding, SecretEncodings.X509, StringComparison.Ordinal)
        )
        {
            return false;
        }

        // An algorithm may accept several key types (ADR-1389); a key of another type than the one configured does not
        // satisfy the specification, so the configured type is provisioned.
        if (!string.Equals(secret.SecretType, specification.SecretType, StringComparison.Ordinal))
        {
            return false;
        }

        if (!SecretKeyTypeCatalog.TryGet(secret.SecretType, out var descriptor))
        {
            return false;
        }

        var collection = AlgorithmCollectionProvider.Collection;
        var declarationError = collection.ValidateKeyAlgorithm(
            new KeyAlgorithmDeclaration
            {
                KeyType = descriptor.KeyClrType,
                KeySizeBits = secret.KeySizeBits,
                Use = secret.Use,
                Algorithm = specification.Algorithm,
            }
        );
        if (declarationError is not null)
        {
            return false;
        }

        // See ADR-1391: two curves can share a key size, as P-256 and secp256k1 do, so the type and size alone do not
        // show that the algorithm accepts the key. The stored public half answers it; a key held without one is trusted
        // as above.
        if (secret.PublicKey is null)
        {
            return true;
        }

        return TryGetKeyedAlgorithm(collection, specification.Algorithm, out var algorithm, out _)
            && IsKeyCompatible(algorithm, secret);
    }

    private bool IsKeyCompatible(KeyedAlgorithm algorithm, PersistedSecret secret)
    {
        SecretKey publicKey;
        try
        {
            publicKey = SecretSerializer.DeserializePublicSecret(secret);
        }
        catch (Exception exception)
            when (exception
                    is CryptographicException
                        or ArgumentException
                        or InvalidOperationException
                        or FormatException
            )
        {
            // A stored public key that cannot be read cannot be shown to fit the algorithm, so a key is provisioned.
            return false;
        }

        return algorithm.IsKeyCompatible(publicKey);
    }

    private static bool TryGetKeyedAlgorithm(
        IAlgorithmCollection collection,
        string algorithmCode,
        [NotNullWhen(true)] out KeyedAlgorithm? algorithm,
        [NotNullWhen(true)] out string? use
    )
    {
        if (collection.TryGetSignatureAlgorithm(algorithmCode, out var signatureAlgorithm))
        {
            algorithm = signatureAlgorithm;
            use = SecretKeyUses.Signature;
            return true;
        }

        if (collection.TryGetKeyManagementAlgorithm(algorithmCode, out var keyManagementAlgorithm))
        {
            algorithm = keyManagementAlgorithm;
            use = SecretKeyUses.Encryption;
            return true;
        }

        algorithm = null;
        use = null;
        return false;
    }

    private bool TryResolveSpecification(
        IAlgorithmCollection collection,
        OpenIdTenantKeyOptions key,
        out TenantKeySpecification specification,
        [NotNullWhen(false)] out string? error
    )
    {
        var algorithmCode = key.Algorithm;
        specification = default;

        if (!TryGetKeyedAlgorithm(collection, algorithmCode, out var algorithm, out var use))
        {
            error =
                $"The algorithm '{algorithmCode}' is not a registered signature or key-management algorithm.";
            return false;
        }

        if (
            !TryResolveKeyType(
                algorithm,
                key,
                out var descriptor,
                out var algorithmKeyType,
                out error
            )
        )
        {
            return false;
        }

        // A tenant key is published in the tenant's JSON Web Key Set and used across trust boundaries, which only an
        // asymmetric key can be.
        if (!descriptor.IsAsymmetric)
        {
            error =
                $"The algorithm '{algorithmCode}' uses a symmetric key, and a tenant is provisioned with asymmetric "
                + "keys only.";
            return false;
        }

        int keySizeBits;
        if (key.KeySizeBits is { } configuredKeySizeBits)
        {
            keySizeBits = configuredKeySizeBits;
        }
        else if (algorithmKeyType.KeyBitSizes.Count > 0)
        {
            keySizeBits = algorithmKeyType.KeyBitSizes.Min(legalSize => legalSize.MinSize);
        }
        else
        {
            error = $"The algorithm '{algorithmCode}' accepts no key size.";
            return false;
        }

        var declarationError = collection.ValidateKeyAlgorithm(
            new KeyAlgorithmDeclaration
            {
                KeyType = descriptor.KeyClrType,
                KeySizeBits = keySizeBits,
                Use = use,
                Algorithm = algorithmCode,
            }
        );
        if (declarationError is not null)
        {
            error = declarationError;
            return false;
        }

        // See ADR-1391: an algorithm bound to one curve names it, so the key is generated on that curve rather than on
        // whichever curve the key type picks for the size.
        var generationError = descriptor.ValidateGeneration(
            new SecretKeyGenerationParameters
            {
                KeySizeBits = keySizeBits,
                Curve = algorithmKeyType.Curve,
            }
        );
        if (generationError is not null)
        {
            error =
                $"A key for the algorithm '{algorithmCode}' cannot be generated: {generationError}";
            return false;
        }

        specification = new TenantKeySpecification
        {
            Algorithm = algorithmCode,
            Use = use,
            SecretType = descriptor.KeyType,
            KeySizeBits = keySizeBits,
            Curve = algorithmKeyType.Curve,
        };
        error = null;
        return true;
    }

    // See ADR-1389: an algorithm may accept several key types. The configured secret type selects one; otherwise the key
    // is generated as the first key type the algorithm lists that has a registered secret key type.
    private bool TryResolveKeyType(
        KeyedAlgorithm algorithm,
        OpenIdTenantKeyOptions key,
        [NotNullWhen(true)] out ISecretKeyTypeDescriptor? descriptor,
        [NotNullWhen(true)] out AlgorithmKeyType? algorithmKeyType,
        [NotNullWhen(false)] out string? error
    )
    {
        if (key.SecretType is { } secretType)
        {
            if (!SecretKeyTypeCatalog.TryGet(secretType, out descriptor))
            {
                algorithmKeyType = null;
                error = $"The secret type '{secretType}' is not a registered secret key type.";
                return false;
            }

            if (!algorithm.TryGetKeyType(descriptor.KeyClrType, out algorithmKeyType))
            {
                error = $"The algorithm '{algorithm.Code}' does not accept '{secretType}' keys.";
                return false;
            }

            error = null;
            return true;
        }

        foreach (var candidateKeyType in algorithm.KeyTypes)
        {
            var candidate = SecretKeyTypeCatalog.Descriptors.FirstOrDefault(candidate =>
                candidate.KeyClrType == candidateKeyType.KeyType
            );
            if (candidate is not null)
            {
                descriptor = candidate;
                algorithmKeyType = candidateKeyType;
                error = null;
                return true;
            }
        }

        descriptor = null;
        algorithmKeyType = null;
        error =
            $"The algorithm '{algorithm.Code}' uses a key type that has no registered secret key type.";
        return false;
    }
}
