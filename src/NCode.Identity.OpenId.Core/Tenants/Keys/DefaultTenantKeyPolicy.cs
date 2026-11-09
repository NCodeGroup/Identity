// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.RemoteKeys;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Tenants.Keys;

/// <summary>
/// Provides the default implementation of <see cref="ITenantKeyPolicy"/>. A configured key names only its algorithm;
/// the key's use and type follow from the registered algorithm and key types, so any registered asymmetric signature or
/// key-management algorithm can be provisioned without this policy knowing it. Every described remote key provider
/// must be able to hold every provisioned key, so a tenant that opts into one is never left without a key.
/// </summary>
// See ADR-1169 (every tenant is provisioned with persisted keys).
internal class DefaultTenantKeyPolicy(
    IAlgorithmCollectionProvider algorithmCollectionProvider,
    ISecretKeyTypeCatalog secretKeyTypeCatalog,
    ISecretSerializer secretSerializer,
    IRemoteKeyDescriptorCatalog remoteKeyDescriptorCatalog
) : ITenantKeyPolicy
{
    // RSA keys are 3072 bits because NIST SP 800-57 accepts 2048-bit RSA only through 2030, and a tenant may set a key
    // lifetime that reaches past it.
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

        if (!descriptor.IsAsymmetric)
        {
            error = FormatSymmetricKeyError(algorithmCode);
            return false;
        }

        // See ADR-1389, ADR-1392 and ADR-1398: two curves can share a key size, so the key is generated on the
        // configured curve, or on the one curve the algorithm accepts that has the configured size, never on whichever
        // curve the key type picks for the size; a size two of the algorithm's curves share is refused.
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

    /// <summary>
    /// Determines whether a key type of the algorithm accepts the configured curve and size, before either is resolved
    /// onto a key: a configured curve must be one of its curves, and a configured size, without a curve, one of its
    /// sizes.
    /// </summary>
    private static bool AcceptsConfiguredKey(
        AlgorithmKeyType algorithmKeyType,
        OpenIdTenantKeyOptions key
    ) =>
        key.Curve is { } curve ? algorithmKeyType.TryGetCurve(curve, out _)
        : key.KeySizeBits is { } keySizeBits ? algorithmKeyType.IsLegalSize(keySizeBits)
        : true;

    private static string FormatList(IEnumerable<string> values)
    {
        var quoted = values.Select(value => $"'{value}'").ToList();
        return quoted.Count == 0 ? "none" : string.Join(", ", quoted);
    }

    // A tenant key is published in the tenant's JSON Web Key Set and used across trust boundaries, which only an
    // asymmetric key can be.
    private static string FormatSymmetricKeyError(string algorithmCode) =>
        $"The algorithm '{algorithmCode}' uses a symmetric key, and a tenant is provisioned with asymmetric keys "
        + "only.";

    // Formats the error for a configured curve or size that none of the algorithm's provisionable key types accepts:
    // the curves, or the sizes, that each of them accepts, so an algorithm that accepts several key types, such as
    // ECDH-ES over EC and OKP keys, reports every curve it accepts (ADR-1398).
    private static string FormatConfiguredKeyError(
        string algorithmCode,
        OpenIdTenantKeyOptions key,
        IEnumerable<(
            AlgorithmKeyType AlgorithmKeyType,
            ISecretKeyTypeDescriptor Descriptor
        )> keyTypes
    ) =>
        key.Curve is { } curve
            ? $"The curve '{curve}' is not valid for algorithm '{algorithmCode}'; expected "
                + FormatAcceptedKeys(keyTypes, FormatAcceptedCurves)
                + "."
            : $"The key size '{key.KeySizeBits}' bits is not valid for algorithm '{algorithmCode}'; expected "
                + FormatAcceptedKeys(keyTypes, FormatAcceptedSizes)
                + ".";

    private static string FormatAcceptedKeys(
        IEnumerable<(
            AlgorithmKeyType AlgorithmKeyType,
            ISecretKeyTypeDescriptor Descriptor
        )> keyTypes,
        Func<AlgorithmKeyType, string> formatAccepted
    ) =>
        string.Join(
            "; ",
            keyTypes.Select(keyType =>
                formatAccepted(keyType.AlgorithmKeyType)
                + $" for '{keyType.Descriptor.KeyType}' keys"
            )
        );

    // The curves a key type accepts, each with its size, or no curve for a key type without curves, which a
    // configured curve never fits.
    private static string FormatAcceptedCurves(AlgorithmKeyType algorithmKeyType) =>
        algorithmKeyType.Curves.Count > 0 ? FormatCurves(algorithmKeyType) : "no curve";

    // The curves a key type accepts, each with its size, which a configured size selects among, or the sizes of a key
    // type without curves.
    private static string FormatAcceptedSizes(AlgorithmKeyType algorithmKeyType) =>
        algorithmKeyType.Curves.Count > 0
            ? FormatCurves(algorithmKeyType)
            : string.Join(", ", algorithmKeyType.KeyBitSizes.Select(FormatKeySizes));

    private static string FormatCurves(AlgorithmKeyType algorithmKeyType) =>
        string.Join(
            ", ",
            algorithmKeyType.Curves.Select(curve => $"'{curve.Name}' ({curve.KeySizeBits} bits)")
        );

    private static string FormatKeySizes(KeySizes keySizes) =>
        keySizes.MinSize == keySizes.MaxSize ? $"{keySizes.MinSize} bits"
        : keySizes.MaxSize == int.MaxValue ? $"{keySizes.MinSize} bits or more"
        : keySizes.SkipSize == 0 ? $"{keySizes.MinSize} to {keySizes.MaxSize} bits"
        : $"{keySizes.MinSize} to {keySizes.MaxSize} bits in steps of {keySizes.SkipSize}";

    private IAlgorithmCollectionProvider AlgorithmCollectionProvider { get; } =
        algorithmCollectionProvider;

    private ISecretKeyTypeCatalog SecretKeyTypeCatalog { get; } = secretKeyTypeCatalog;

    private ISecretSerializer SecretSerializer { get; } = secretSerializer;

    private IRemoteKeyDescriptorCatalog RemoteKeyDescriptorCatalog { get; } =
        remoteKeyDescriptorCatalog;

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

            AddUnsupportedProviderErrors(specifications, errors);
        }

        return new TenantKeySpecificationResolution
        {
            Specifications = specifications,
            Errors = errors,
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<TenantKeySpecification> ForTenant(
        IReadOnlyList<TenantKeySpecification> specifications,
        IReadOnlySettingCollection settings
    )
    {
        ArgumentNullException.ThrowIfNull(specifications);
        ArgumentNullException.ThrowIfNull(settings);

        // See ADR-1471 and ADR-1477: an empty setting keeps every key in the server's store; otherwise the provider holds
        // every provisioned key.
        if (
            !settings.TryGetValue(OpenIdSettingKeys.TenantKeyProvider, out var provider)
            || string.IsNullOrEmpty(provider)
        )
        {
            return specifications;
        }

        return
        [
            .. specifications.Select(specification =>
                specification with
                {
                    RemoteKeyProvider = provider,
                }
            ),
        ];
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

        // The key is held by the custody the specification names, so a tenant that changes its custody is provisioned
        // keys of the new one.
        if (!HasCustody(specification, secret))
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

    // A tenant that opts into a provider has every provisioned key held there, so a provider without a row for one of
    // them, or whose row holds the key for another use, stops the host instead of leaving the tenant's seed or
    // rollover without that key (ADR-1471, ADR-1477). A provider that creates no keys never holds a provisioned key,
    // so it is not checked.
    private void AddUnsupportedProviderErrors(
        List<TenantKeySpecification> specifications,
        List<string> errors
    )
    {
        var providerNames = RemoteKeyDescriptorCatalog
            .ProvidersThatCreateKeys.Select(provider => provider.Name)
            .Order(StringComparer.Ordinal);

        foreach (var name in providerNames)
        {
            foreach (var specification in specifications)
            {
                var selector = new RemoteKeyAlgorithmSelector
                {
                    Provider = name,
                    Algorithm = specification.Algorithm,
                    SecretType = specification.SecretType,
                    KeySizeBits = specification.KeySizeBits,
                    Curve = specification.Curve,
                };

                var curve = specification.Curve is null
                    ? string.Empty
                    : $", curve '{specification.Curve}'";

                if (!RemoteKeyDescriptorCatalog.TryGetAlgorithm(selector, out var row))
                {
                    errors.Add(
                        $"The remote key provider '{name}' cannot hold the provisioned '{specification.Algorithm}' key "
                            + $"({specification.SecretType}, {specification.KeySizeBits} bits{curve})."
                    );
                }
                else if (!string.Equals(row.Use, specification.Use, StringComparison.Ordinal))
                {
                    errors.Add(
                        $"The remote key provider '{name}' holds '{specification.Algorithm}' keys for the use '{row.Use}', "
                            + $"not for the provisioned key's use '{specification.Use}' "
                            + $"({specification.SecretType}, {specification.KeySizeBits} bits{curve})."
                    );
                }
            }
        }
    }

    // A remote specification is satisfied only by a remote secret whose reference names its provider, and a local one
    // only by a secret that is not remote.
    private static bool HasCustody(TenantKeySpecification specification, PersistedSecret secret)
    {
        var isRemote = SecretEncodings.IsRemote(secret.SecretEncoding);

        return specification.RemoteKeyProvider is not { } provider
            ? !isRemote
            : isRemote
                && RemoteKeyReference.TryParse(secret.EncodedValue, out var reference)
                && string.Equals(reference.Provider, provider, StringComparison.Ordinal);
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

        // See ADR-1389 and ADR-1398: an algorithm may accept several key types. Unless the configured secret type selects
        // one, the key is generated as the first key type the algorithm lists that has a registered asymmetric secret key
        // type and that accepts the configured curve and size, so a key type that cannot be provisioned yields to the
        // next, and that key type's resolution is the outcome: a configuration it refuses, such as a size two of its
        // curves share, is reported, never provisioned as another key type.
        var keyTypes =
            new List<(AlgorithmKeyType AlgorithmKeyType, ISecretKeyTypeDescriptor Descriptor)>();
        foreach (var algorithmKeyType in algorithm.KeyTypes)
        {
            var descriptor = SecretKeyTypeCatalog.Descriptors.FirstOrDefault(candidate =>
                candidate.KeyClrType == algorithmKeyType.KeyClrType
            );
            if (descriptor is not null)
            {
                keyTypes.Add((algorithmKeyType, descriptor));
            }
        }

        if (keyTypes.Count == 0)
        {
            error =
                $"The algorithm '{algorithmCode}' uses a key type that has no registered secret key type.";
            return false;
        }

        var asymmetricKeyTypes = keyTypes
            .Where(keyType => keyType.Descriptor.IsAsymmetric)
            .ToList();
        if (asymmetricKeyTypes.Count == 0)
        {
            error = FormatSymmetricKeyError(algorithmCode);
            return false;
        }

        foreach (var (algorithmKeyType, descriptor) in asymmetricKeyTypes)
        {
            if (AcceptsConfiguredKey(algorithmKeyType, key))
            {
                return TryBuildSpecification(
                    collection,
                    key,
                    descriptor,
                    out specification,
                    out error
                );
            }
        }

        // No key type accepts the configured curve or size, so the error lists what every provisionable key type of
        // the algorithm accepts.
        error = FormatConfiguredKeyError(algorithmCode, key, asymmetricKeyTypes);
        return false;
    }
}
