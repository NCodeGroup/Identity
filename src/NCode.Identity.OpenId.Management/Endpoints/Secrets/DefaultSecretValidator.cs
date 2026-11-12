// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.OpenId.Management.Contracts.Secrets;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.OpenId.Management.Endpoints.Secrets;

/// <summary>
/// Provides the default implementation of <see cref="ISecretValidator"/> that rejects incoherent
/// <c>secretType</c> / <c>keySizeBits</c> / <c>use</c> / <c>algorithm</c> combinations with a
/// deterministic <c>400</c> before any key material is generated.
/// </summary>
/// <remarks>
/// The key-type taxonomy and intrinsic per-type key-size rules come from the <see cref="ISecretKeyTypeCatalog"/>
/// (the single source of truth), so the validator cannot drift from the types the generator can actually
/// produce. Algorithm compatibility is resolved against the live <see cref="IAlgorithmCollectionProvider"/> rather
/// than a hardcoded table, so the validator also cannot drift from the set of algorithms the server actually
/// supports (including any custom algorithms an integrator registers): the same <see cref="KeyedAlgorithm"/>
/// metadata the crypto runtime uses to validate a key is what gates its creation here.
/// </remarks>
// See ADR-1067.
// See ADR-1474.
// See ADR-1476.
internal sealed class DefaultSecretValidator(
    IAlgorithmCollectionProvider algorithmCollectionProvider,
    ISecretKeyTypeCatalog secretKeyTypeCatalog,
    ISecretSerializer secretSerializer,
    IRemoteKeyDescriptorCatalog remoteKeyDescriptorCatalog
) : ISecretValidator
{
    private static ManagementError Error(string detail) =>
        new() { StatusCode = StatusCodes.Status400BadRequest, Detail = detail };

    private static SecretCreateValidation Invalid(string detail) =>
        SecretCreateValidation.Invalid(Error(detail));

    /// <summary>
    /// Checks the instants of a secret's lifecycle a create or import request names, resolved and judged by the one
    /// rule the generator and the importer apply, so a request they would refuse is answered <c>400</c> with the
    /// reason.
    /// </summary>
    /// <param name="createdWhen">The instant the secret is created.</param>
    /// <param name="activatesWhen">The instant the request activates the secret, or <c>null</c> for its creation.</param>
    /// <param name="retiresWhen">The instant the request retires the secret, or <c>null</c> for its expiry.</param>
    /// <param name="expiresWhen">The instant the secret expires.</param>
    /// <returns>The error that names the instants out of order, or <c>null</c> when they are in order.</returns>
    // See ADR-1403.
    private static ManagementError? ValidateLifecycle(
        DateTimeOffset createdWhen,
        DateTimeOffset? activatesWhen,
        DateTimeOffset? retiresWhen,
        DateTimeOffset expiresWhen
    ) =>
        SecretLifecycle.TryResolve(
            createdWhen,
            activatesWhen,
            retiresWhen,
            expiresWhen,
            out _,
            out var error
        )
            ? null
            : Error(error);

    private IAlgorithmCollectionProvider AlgorithmCollectionProvider { get; } =
        algorithmCollectionProvider;

    private ISecretKeyTypeCatalog SecretKeyTypeCatalog { get; } = secretKeyTypeCatalog;

    private ISecretSerializer SecretSerializer { get; } = secretSerializer;

    private IRemoteKeyDescriptorCatalog RemoteKeyDescriptorCatalog { get; } =
        remoteKeyDescriptorCatalog;

    /// <inheritdoc />
    // See ADR-1389 and ADR-1392.
    public SecretCreateValidation ValidateCreate(
        CreateSecretRequest request,
        DateTimeOffset createdWhen
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        var lifecycleError = ValidateLifecycle(
            createdWhen,
            request.ActivatesWhen,
            request.RetiresWhen,
            request.ExpiresWhen
        );
        if (lifecycleError is not null)
        {
            return SecretCreateValidation.Invalid(lifecycleError);
        }

        var secretType = request.SecretType;
        if (!SecretKeyTypeCatalog.TryGet(secretType, out var descriptor))
        {
            return Invalid(
                $"The secret type '{secretType}' is not supported or cannot be created through the "
                    + "management API."
            );
        }

        var use = request.Use;
        if (use is not null && use is not (SecretKeyUses.Signature or SecretKeyUses.Encryption))
        {
            return Invalid(
                $"The use '{use}' is not supported; expected "
                    + $"'{SecretKeyUses.Signature}' or '{SecretKeyUses.Encryption}'."
            );
        }

        var requested = new RequestedKeyGeneration
        {
            KeySizeBits = request.KeySizeBits,
            Curve = request.Curve,
            Use = use,
            Algorithm = request.Algorithm,
        };
        return ResolveGeneration(descriptor, requested, out _);
    }

    /// <inheritdoc />
    // See ADR-1475.
    public SecretCreateValidation ValidateCreateRemote(
        CreateRemoteSecretRequest request,
        DateTimeOffset createdWhen
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        var lifecycleError = ValidateLifecycle(
            createdWhen,
            request.ActivatesWhen,
            request.RetiresWhen,
            request.ExpiresWhen
        );
        if (lifecycleError is not null)
        {
            return SecretCreateValidation.Invalid(lifecycleError);
        }

        var selector = new RemoteKeyAlgorithmSelector
        {
            Provider = request.RemoteKeyProvider,
            Algorithm = request.Algorithm,
            SecretType = request.SecretType,
            KeySizeBits = request.KeySizeBits,
            Curve = request.Curve,
        };
        var validation = ValidateRemoteKey(selector);
        if (validation.IsValid && !RemoteKeyDescriptorCatalog.CreatesKeys(selector.Provider))
        {
            return Invalid(RemoteKeyRefusals.CreatesNoKeys(selector.Provider));
        }

        return validation;
    }

    /// <inheritdoc />
    // See ADR-1476.
    // See ADR-1477.
    public SecretCreateValidation ValidateRemoteKey(RemoteKeyAlgorithmSelector selector)
    {
        var secretType = selector.SecretType;
        if (
            !SecretKeyTypeCatalog.TryGet(secretType, out var descriptor)
            || descriptor is not IRemoteSecretKeyTypeDescriptor
        )
        {
            return Invalid(
                $"The secret type '{secretType}' cannot be held in a key management service."
            );
        }

        if (string.IsNullOrEmpty(selector.Provider))
        {
            return Invalid(DescribeUnregisteredProvider(providerName: null));
        }

        if (!RemoteKeyDescriptorCatalog.TryGetProvider(selector.Provider, out _))
        {
            return Invalid(DescribeUnregisteredProvider(selector.Provider));
        }

        var requested = new RequestedKeyGeneration
        {
            KeySizeBits = selector.KeySizeBits,
            Curve = selector.Curve,
            Use = null,
            Algorithm = selector.Algorithm,
        };
        var validation = ResolveGeneration(descriptor, requested, out var impliedUse);
        return ValidateRemoteRow(selector, validation, impliedUse);
    }

    private SecretCreateValidation ValidateRemoteRow(
        RemoteKeyAlgorithmSelector selector,
        SecretCreateValidation validation,
        string? impliedUse
    )
    {
        if (!validation.IsValid)
        {
            return validation;
        }

        var resolved = validation.GenerationParameters;
        var resolvedSelector = selector with
        {
            KeySizeBits = resolved.KeySizeBits,
            Curve = resolved.Curve,
        };
        if (!RemoteKeyDescriptorCatalog.TryGetAlgorithm(resolvedSelector, out var row))
        {
            var curveText = resolved.Curve is null
                ? string.Empty
                : $" on the curve '{resolved.Curve}'";
            return Invalid(
                $"The remote key provider '{selector.Provider}' holds no '{selector.Algorithm}' key of the "
                    + $"'{selector.SecretType}' type with {resolved.KeySizeBits} bits{curveText}."
            );
        }

        // See ADR-1477.
        if (!string.Equals(row.Use, impliedUse, StringComparison.Ordinal))
        {
            return Invalid(
                $"The remote key provider '{selector.Provider}' holds '{selector.Algorithm}' keys for the use "
                    + $"'{row.Use}', but the algorithm implies the use '{impliedUse}'."
            );
        }

        return validation;
    }

    private string DescribeUnregisteredProvider(string? providerName)
    {
        var subject = providerName is null
            ? "The remote key provider is not named"
            : $"The remote key provider '{providerName}' is not registered";
        var names = RemoteKeyDescriptorCatalog
            .Providers.Select(provider => provider.Name)
            .Order(StringComparer.Ordinal)
            .ToList();
        return names.Count == 0
            ? $"{subject}; the deployment registers no remote key provider."
            : $"{subject}; expected " + string.Join(", ", names.Select(name => $"'{name}'")) + ".";
    }

    private SecretCreateValidation ResolveGeneration(
        ISecretKeyTypeDescriptor descriptor,
        RequestedKeyGeneration requested,
        out string? impliedUse
    )
    {
        impliedUse = null;
        var keySizeBits = requested.KeySizeBits;
        var curve = requested.Curve;
        var use = requested.Use;
        var algorithm = requested.Algorithm;

        // When an algorithm is specified, its registered metadata is authoritative for the use, the key type, the legal
        // key sizes and the curve the key is generated on: two curves can share a key size, so the key is generated on
        // the requested curve or on the one curve the algorithm accepts that has the requested size, never on whichever
        // curve the key type picks for the size (ADR-1398).
        var parameters = new SecretKeyGenerationParameters
        {
            KeySizeBits = keySizeBits,
            Curve = curve,
        };
        if (algorithm is not null)
        {
            if (
                !AlgorithmCollectionProvider.Collection.TryResolveKeyGeneration(
                    new KeyGenerationDeclaration
                    {
                        KeyClrType = descriptor.KeyClrType,
                        KeySizeBits = keySizeBits,
                        Curve = curve,
                        Use = use,
                        Algorithm = algorithm,
                    },
                    out var resolved,
                    out var algorithmError
                )
            )
            {
                return Invalid(algorithmError);
            }

            impliedUse = resolved.Use;
            parameters = new SecretKeyGenerationParameters
            {
                KeySizeBits = resolved.KeySizeBits,
                Curve = resolved.Curve,
            };
        }

        // The descriptor validates the intrinsic generation parameters: the key size and, for a key type with curves,
        // the curve and that the size is the curve's.
        var generationError = descriptor.ValidateGeneration(parameters);
        return generationError is null
            ? SecretCreateValidation.Valid(parameters)
            : Invalid(generationError);
    }

    /// <inheritdoc />
    public ManagementError? ValidateImport(ImportSecretRequest request, DateTimeOffset createdWhen)
    {
        ArgumentNullException.ThrowIfNull(request);

        var publicKeyError = ValidatePublicKeyImport(
            request.SecretType,
            request.Format,
            request.KeyMaterial
        );
        if (publicKeyError is not null)
        {
            return publicKeyError;
        }

        var use = request.Use;
        if (use is not null && use is not (SecretKeyUses.Signature or SecretKeyUses.Encryption))
        {
            return Error(
                $"The use '{use}' is not supported; expected "
                    + $"'{SecretKeyUses.Signature}' or '{SecretKeyUses.Encryption}'."
            );
        }

        return ValidateLifecycle(
            createdWhen,
            request.ActivatesWhen,
            request.RetiresWhen,
            request.ExpiresWhen
        );
    }

    /// <inheritdoc />
    // See ADR-1475.
    // See ADR-1476.
    public SecretCreateValidation ValidateImportRemote(
        ImportRemoteSecretRequest request,
        DateTimeOffset createdWhen
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        var lifecycleError = ValidateLifecycle(
            createdWhen,
            request.ActivatesWhen,
            request.RetiresWhen,
            request.ExpiresWhen
        );
        if (lifecycleError is not null)
        {
            return SecretCreateValidation.Invalid(lifecycleError);
        }

        if (string.IsNullOrWhiteSpace(request.RemoteKeyId))
        {
            return Invalid("The key management service's key identifier must not be empty.");
        }

        var maxKeyIdLength = SecretMaxLengths.EncodedValue - request.RemoteKeyProvider.Length - 1;
        if (request.RemoteKeyId.Length > maxKeyIdLength)
        {
            return Invalid(
                $"The key management service's key identifier must be at most {maxKeyIdLength} characters."
            );
        }

        if (
            request.RemoteKeyId.Any(char.IsControl)
            || request.RemoteKeyId.Trim().Length != request.RemoteKeyId.Length
        )
        {
            return Invalid(
                "The key management service's key identifier must not hold control characters or surrounding white space."
            );
        }

        var selector = new RemoteKeyAlgorithmSelector
        {
            Provider = request.RemoteKeyProvider,
            Algorithm = request.Algorithm,
            SecretType = request.SecretType,
            KeySizeBits = request.KeySizeBits,
            Curve = request.Curve,
        };
        return ValidateRemoteKey(selector);
    }

    private ManagementError? ValidatePublicKeyImport(
        string secretType,
        string format,
        string keyMaterial
    )
    {
        // Each key type declares the formats it parses, so a key type another package registers brings its own
        // (ADR-1150).
        if (
            !SecretKeyTypeCatalog.TryGet(secretType, out var descriptor)
            || descriptor.PublicKeyImportFormats.Count == 0
        )
        {
            return Error(
                $"The secret type '{secretType}' is not supported or cannot be imported as public key material."
            );
        }

        if (!descriptor.PublicKeyImportFormats.Contains(format))
        {
            return Error(
                $"The import format '{format}' is not supported for the secret type '{secretType}'; expected "
                    + string.Join(", ", descriptor.PublicKeyImportFormats.Select(f => $"'{f}'"))
                    + "."
            );
        }

        return string.IsNullOrWhiteSpace(keyMaterial)
            ? Error("The key material must not be empty.")
            : null;
    }

    /// <inheritdoc />
    // See ADR-1161.
    public ManagementError? ValidateUpdate(PersistedSecret secret, UpdateSecretRequest request)
    {
        ArgumentNullException.ThrowIfNull(secret);
        ArgumentNullException.ThrowIfNull(request);

        var use = request.Use;
        if (use is not null && use is not (SecretKeyUses.Signature or SecretKeyUses.Encryption))
        {
            return Error(
                $"The use '{use}' is not supported; expected "
                    + $"'{SecretKeyUses.Signature}' or '{SecretKeyUses.Encryption}'."
            );
        }

        // An update keeps the algorithm of a key that a key management service holds (ADR-1475).
        if (
            SecretEncodings.IsRemote(secret.SecretEncoding)
            && !string.Equals(request.Algorithm, secret.Algorithm, StringComparison.Ordinal)
        )
        {
            return Error(
                $"A key that a key management service holds is bound to its algorithm '{secret.Algorithm}', which an update keeps."
            );
        }

        // An instant the update omits keeps the stored one, so the update is judged as the handler will write it.
        if (
            !SecretLifecycle.TryResolveUpdate(
                secret,
                request.ActivatesWhen,
                request.RetiresWhen,
                request.ExpiresWhen,
                out _,
                out var lifecycleError
            )
        )
        {
            return Error(lifecycleError);
        }

        if (request.Algorithm is not { } algorithm)
            return null;

        if (!SecretKeyTypeCatalog.TryGet(secret.SecretType, out var descriptor))
            return Error($"The secret type '{secret.SecretType}' is not supported.");

        // An asymmetric key is checked by its stored public half, stored as the key itself or beside its private half
        // (ADR-1268), which shows the key's curve that its type and size do not.
        if (descriptor.IsAsymmetric)
        {
            if (!SecretSerializer.TryDeserializePublicSecret(secret, out var publicKey, out _))
            {
                return Error("The key's stored public key cannot be read.");
            }

            var keyError = AlgorithmCollectionProvider.Collection.ValidateKeyAlgorithm(
                new KeyAlgorithmDeclaration
                {
                    Key = publicKey,
                    Use = use,
                    Algorithm = algorithm,
                }
            );
            return keyError is null ? null : Error(keyError);
        }

        // A symmetric key has no curve, and its material is not at hand, so it is checked by its type and size.
        var sizeError = AlgorithmCollectionProvider.Collection.ValidateKeySize(
            new KeySizeDeclaration
            {
                KeyClrType = descriptor.KeyClrType,
                KeySizeBits = secret.KeySizeBits,
                Use = use,
                Algorithm = algorithm,
            }
        );
        return sizeError is null ? null : Error(sizeError);
    }
}
