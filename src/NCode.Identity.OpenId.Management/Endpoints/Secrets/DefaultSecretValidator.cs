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
internal sealed class DefaultSecretValidator(
    IAlgorithmCollectionProvider algorithmCollectionProvider,
    ISecretKeyTypeCatalog secretKeyTypeCatalog,
    ISecretSerializer secretSerializer
) : ISecretValidator
{
    private static ManagementError Error(string detail) =>
        new() { StatusCode = StatusCodes.Status400BadRequest, Detail = detail };

    private static SecretCreateValidation Invalid(string detail) =>
        SecretCreateValidation.Invalid(Error(detail));

    private IAlgorithmCollectionProvider AlgorithmCollectionProvider { get; } =
        algorithmCollectionProvider;

    private ISecretKeyTypeCatalog SecretKeyTypeCatalog { get; } = secretKeyTypeCatalog;

    private ISecretSerializer SecretSerializer { get; } = secretSerializer;

    /// <inheritdoc />
    // The instants of a secret's lifecycle are checked by the one rule the generator and the importer apply, so a
    // request they would refuse is answered 400 with the reason (ADR-1403).
    private static ManagementError? ValidateLifecycle(
        DateTimeOffset createdWhen,
        DateTimeOffset? activatesWhen,
        DateTimeOffset? retiresWhen,
        DateTimeOffset expiresWhen
    ) =>
        SecretLifecycle.TryValidate(
            createdWhen,
            activatesWhen ?? createdWhen,
            retiresWhen ?? expiresWhen,
            expiresWhen,
            out var error
        )
            ? null
            : Error(error);

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

        // When an algorithm is specified, its registered metadata is authoritative for the use, the key type, the legal
        // key sizes and the curve the key is generated on: two curves can share a key size, so the key is generated on
        // the requested curve or on the one curve the algorithm accepts that has the requested size, never on whichever
        // curve the key type picks for the size (ADR-1398).
        var parameters = new SecretKeyGenerationParameters
        {
            KeySizeBits = request.KeySizeBits,
            Curve = request.Curve,
        };
        if (request.Algorithm is { } algorithm)
        {
            if (
                !AlgorithmCollectionProvider.Collection.TryResolveKeyGeneration(
                    new KeyGenerationDeclaration
                    {
                        KeyClrType = descriptor.KeyClrType,
                        KeySizeBits = request.KeySizeBits,
                        Curve = request.Curve,
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

        // Each key type declares the formats it parses, so a key type another package registers brings its own
        // (ADR-1150).
        var secretType = request.SecretType;
        if (
            !SecretKeyTypeCatalog.TryGet(secretType, out var descriptor)
            || descriptor.PublicKeyImportFormats.Count == 0
        )
        {
            return Error(
                $"The secret type '{secretType}' is not supported or cannot be imported as public key material."
            );
        }

        if (!descriptor.PublicKeyImportFormats.Contains(request.Format))
        {
            return Error(
                $"The import format '{request.Format}' is not supported for the secret type '{secretType}'; expected "
                    + string.Join(
                        ", ",
                        descriptor.PublicKeyImportFormats.Select(format => $"'{format}'")
                    )
                    + "."
            );
        }

        if (string.IsNullOrWhiteSpace(request.KeyMaterial))
        {
            return Error("The key material must not be empty.");
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
    // See ADR-1161 and ADR-1186.
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

        var lifecycleError = ValidateLifecycle(
            secret.CreatedWhen,
            request.ActivatesWhen,
            request.RetiresWhen,
            request.ExpiresWhen
        );
        if (lifecycleError is not null)
            return lifecycleError;

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
