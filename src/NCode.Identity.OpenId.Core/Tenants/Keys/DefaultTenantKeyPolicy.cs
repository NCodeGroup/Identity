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

using System.Diagnostics.CodeAnalysis;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Tenants.Keys;

/// <summary>
/// Provides the default implementation of <see cref="ITenantKeyPolicy"/>. A configured key names only its algorithm;
/// the key's use and type follow from the registered algorithm and key types, so any registered asymmetric signature or
/// key-management algorithm can be provisioned without this policy knowing it.
/// </summary>
// See ADR-0169 (every tenant is provisioned with persisted keys).
internal class DefaultTenantKeyPolicy(
    IAlgorithmCollectionProvider algorithmCollectionProvider,
    ISecretKeyTypeCatalog secretKeyTypeCatalog
) : ITenantKeyPolicy
{
    // RSA keys are 3072 bits because NIST SP 800-57 accepts 2048-bit RSA only through 2030, and a provisioned key has
    // no lifetime of its own until key rollover exists (backlog S8).
    private const int DefaultRsaKeySizeBits = 3072;

    private IAlgorithmCollectionProvider AlgorithmCollectionProvider { get; } =
        algorithmCollectionProvider;

    private ISecretKeyTypeCatalog SecretKeyTypeCatalog { get; } = secretKeyTypeCatalog;

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

        if (!SecretKeyTypeCatalog.TryGet(secret.SecretType, out var descriptor))
        {
            return false;
        }

        var declarationError = AlgorithmCollectionProvider.Collection.ValidateKeyAlgorithm(
            new KeyAlgorithmDeclaration
            {
                KeyType = descriptor.KeyClrType,
                KeySizeBits = secret.KeySizeBits,
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

        KeyedAlgorithm algorithm;
        string use;
        if (collection.TryGetSignatureAlgorithm(algorithmCode, out var signatureAlgorithm))
        {
            algorithm = signatureAlgorithm;
            use = SecretKeyUses.Signature;
        }
        else if (
            collection.TryGetKeyManagementAlgorithm(algorithmCode, out var keyManagementAlgorithm)
        )
        {
            algorithm = keyManagementAlgorithm;
            use = SecretKeyUses.Encryption;
        }
        else
        {
            error =
                $"The algorithm '{algorithmCode}' is not a registered signature or key-management algorithm.";
            return false;
        }

        var descriptor = SecretKeyTypeCatalog.Descriptors.FirstOrDefault(candidate =>
            candidate.KeyClrType == algorithm.KeyType
        );
        if (descriptor is null)
        {
            error =
                $"The algorithm '{algorithmCode}' uses a key type that has no registered secret key type.";
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
        else if (algorithm.KeyBitSizes.Any())
        {
            keySizeBits = algorithm.KeyBitSizes.Min(legalSize => legalSize.MinSize);
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

        var generationError = descriptor.ValidateGeneration(
            new SecretKeyGenerationParameters { KeySizeBits = keySizeBits }
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
        };
        error = null;
        return true;
    }
}
