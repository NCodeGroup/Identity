// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
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

    /// <summary>
    /// Builds the specification of a key of one of the algorithm's key types: its size, and the curve it is generated on
    /// when the key type lists curves, as <see cref="AlgorithmCollectionExtensions"/> resolves them.
    /// </summary>
    private static bool TryBuildSpecification(
        IAlgorithmCollection collection,
        OpenIdTenantKeyOptions key,
        ISecretKeyTypeDescriptor descriptor,
        out TenantKeySpecification specification,
        [NotNullWhen(false)] out string? error
    )
    {
        var algorithmCode = key.Algorithm;
        specification = default;

        // A tenant key is published in the tenant's JSON Web Key Set and used across trust boundaries, which only an
        // asymmetric key can be.
        if (!descriptor.IsAsymmetric)
        {
            error =
                $"The algorithm '{algorithmCode}' uses a symmetric key, and a tenant is provisioned with asymmetric "
                + "keys only.";
            return false;
        }

        // See ADR-1389 and ADR-1392: two curves can share a key size, so the key is generated on the configured curve,
        // or on the first curve the algorithm accepts that has the configured size, never on whichever curve the key
        // type picks for the size.
        if (
            !collection.TryResolveKeyGeneration(
                new KeyGenerationDeclaration
                {
                    KeyClrType = descriptor.KeyClrType,
                    KeySizeBits = key.KeySizeBits,
                    Curve = key.Curve,
                    Algorithm = algorithmCode,
                },
                out var resolved,
                out var resolutionError
            )
        )
        {
            error = resolutionError;
            return false;
        }

        var generationError = descriptor.ValidateGeneration(
            new SecretKeyGenerationParameters
            {
                KeySizeBits = resolved.KeySizeBits,
                Curve = resolved.Curve,
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
            Use = resolved.Use,
            SecretType = descriptor.KeyType,
            KeySizeBits = resolved.KeySizeBits,
            Curve = resolved.Curve,
        };
        error = null;
        return true;
    }

    private static string FormatList(IEnumerable<string> values)
    {
        var quoted = values.Select(value => $"'{value}'").ToList();
        return quoted.Count == 0 ? "none" : string.Join(", ", quoted);
    }

    private IAlgorithmCollectionProvider AlgorithmCollectionProvider { get; } =
        algorithmCollectionProvider;

    private ISecretKeyTypeCatalog SecretKeyTypeCatalog { get; } = secretKeyTypeCatalog;

    private ISecretSerializer SecretSerializer { get; } = secretSerializer;

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

        // A key bound to another algorithm is never selected for this one; an unbound key serves any algorithm that
        // accepts it, as the runtime credential selector treats it.
        if (
            secret.Algorithm is not null
            && !string.Equals(secret.Algorithm, specification.Algorithm, StringComparison.Ordinal)
        )
        {
            return false;
        }

        // Public material cannot sign or decrypt.
        if (SecretEncodings.IsPublic(secret.SecretEncoding))
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

        // See ADR-1392: two curves can share a key size, as P-256 and secp256k1 do, so the type and size alone do not
        // show that the algorithm accepts the key. Every private asymmetric secret stores its public half (ADR-1268),
        // which shows its curve; a secret whose public half is missing or cannot be read cannot be shown to fit, so a
        // key is provisioned for the specification.
        if (
            !descriptor.IsAsymmetric
            || secret.PublicKey is null
            || !SecretSerializer.TryDeserializePublicSecret(secret, out var publicKey, out _)
        )
        {
            return false;
        }

        // An algorithm may accept several curves, and the specification names the one its key is on: the configured
        // curve, or the one the configured size or the algorithm selects. A key on another curve the algorithm accepts is
        // not the key the specification names, so a key on the specification's curve is provisioned.
        if (
            specification.Curve is not null
            && !string.Equals(
                (publicKey as AsymmetricSecretKey)?.CurveName,
                specification.Curve,
                StringComparison.Ordinal
            )
        )
        {
            return false;
        }

        var declarationError = AlgorithmCollectionProvider.Collection.ValidateKeyAlgorithm(
            new KeyAlgorithmDeclaration
            {
                Key = publicKey,
                Use = secret.Use,
                Algorithm = specification.Algorithm,
            }
        );
        return declarationError is null;
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

        if (!collection.TryGetKeyedAlgorithm(algorithmCode, out var algorithm, out _))
        {
            error =
                $"The algorithm '{algorithmCode}' is not a registered signature or key-management algorithm.";
            return false;
        }

        if (key.SecretType is { } secretType)
        {
            if (!SecretKeyTypeCatalog.TryGet(secretType, out var descriptor))
            {
                error =
                    $"The secret type '{secretType}' is not a registered secret key type; expected "
                    + FormatList(
                        SecretKeyTypeCatalog.Descriptors.Select(candidate => candidate.KeyType)
                    )
                    + ".";
                return false;
            }

            if (!algorithm.TryGetKeyType(descriptor.KeyClrType, out _))
            {
                error =
                    $"The algorithm '{algorithmCode}' does not accept '{secretType}' keys; expected "
                    + FormatList(
                        SecretKeyTypeCatalog
                            .Descriptors.Where(candidate =>
                                algorithm.TryGetKeyType(candidate.KeyClrType, out _)
                            )
                            .Select(candidate => candidate.KeyType)
                    )
                    + ".";
                return false;
            }

            return TryBuildSpecification(collection, key, descriptor, out specification, out error);
        }

        // See ADR-1389: an algorithm may accept several key types. Unless the configured secret type selects one, the
        // key is generated as the first key type the algorithm lists that has a registered secret key type and that a
        // key can be generated as, so a key type that cannot be provisioned yields to the next.
        string? firstError = null;
        foreach (var algorithmKeyType in algorithm.KeyTypes)
        {
            var descriptor = SecretKeyTypeCatalog.Descriptors.FirstOrDefault(candidate =>
                candidate.KeyClrType == algorithmKeyType.KeyClrType
            );
            if (descriptor is null)
            {
                continue;
            }

            if (
                TryBuildSpecification(
                    collection,
                    key,
                    descriptor,
                    out specification,
                    out var candidateError
                )
            )
            {
                error = null;
                return true;
            }

            firstError ??= candidateError;
        }

        error =
            firstError
            ?? $"The algorithm '{algorithmCode}' uses a key type that has no registered secret key type.";
        return false;
    }
}
