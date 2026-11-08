// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Management.Contracts.Secrets;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Management.Endpoints.Secrets;

/// <summary>
/// Validates the intrinsic coherence of a secret-create, import, or update request — the <c>secretType</c>,
/// <c>keySizeBits</c>, <c>use</c>, and <c>algorithm</c> parameters must form a compatible combination.
/// Register a custom implementation to replace or extend the default validation.
/// </summary>
/// <remarks>
/// This validator asserts only the parameter-level coherence that is independent of the owning
/// tenant, client or resource server; authorization and existence preconditions remain the responsibility of the
/// calling endpoint.
/// </remarks>
[PublicAPI]
public interface ISecretValidator
{
    /// <summary>
    /// Validates that the specified <see cref="CreateSecretRequest"/> describes a coherent secret, and resolves the
    /// parameters the key is generated with. A request that names an algorithm bound to curves, such as <c>ES256K</c>,
    /// and no curve resolves to the one curve the algorithm accepts that has the requested size, so the key is never
    /// generated on the curve the key type picks for the size, and a size that two of the algorithm's curves have is
    /// refused until the request names its curve. A named curve whose keys are not the requested size is refused. The
    /// caller generates the key with <see cref="SecretCreateValidation.GenerationParameters"/>.
    /// </summary>
    /// <param name="request">The <see cref="CreateSecretRequest"/> to validate.</param>
    /// <param name="createdWhen">The instant the secret is created at, which its activation cannot precede.</param>
    /// <returns>The <see cref="SecretCreateValidation"/>: the generation parameters, or the first failed
    /// check.</returns>
    SecretCreateValidation ValidateCreate(CreateSecretRequest request, DateTimeOffset createdWhen);

    /// <summary>
    /// Validates that the specified <see cref="ImportSecretRequest"/> describes a coherent public-key import. The
    /// supplied key material itself is parsed and validated by the importer; this check asserts only the
    /// parameter-level coherence (recognized <c>format</c>, non-empty material, supported <c>use</c>, and the instants of
    /// the key's lifecycle in order).
    /// </summary>
    /// <param name="request">The <see cref="ImportSecretRequest"/> to validate.</param>
    /// <param name="createdWhen">The instant the secret is created at, which its activation cannot precede.</param>
    /// <returns>A <see cref="ManagementError"/> describing the first failed check, or <c>null</c> when valid.</returns>
    ManagementError? ValidateImport(ImportSecretRequest request, DateTimeOffset createdWhen);

    /// <summary>
    /// Validates that the specified <see cref="CreateRemoteSecretRequest"/> describes a key a registered remote key
    /// provider can create, and resolves the parameters the key is generated with. Everything is checked before the
    /// key management service is called: the lifecycle, a key type a key management service can hold, a registered
    /// provider, the generation parameters the signature algorithm resolves, and a row of the provider's mapping
    /// vocabulary for the provider, algorithm, type, size and curve.
    /// </summary>
    /// <param name="request">The <see cref="CreateRemoteSecretRequest"/> to validate.</param>
    /// <param name="createdWhen">The instant the secret is created at, which its activation cannot precede.</param>
    /// <returns>The <see cref="SecretCreateValidation"/>: the generation parameters, or the first failed
    /// check.</returns>
    SecretCreateValidation ValidateCreateRemote(
        CreateRemoteSecretRequest request,
        DateTimeOffset createdWhen
    );

    /// <summary>
    /// Validates that the specified <see cref="ImportRemoteSecretRequest"/> describes a coherent binding of a key a key
    /// management service holds: the lifecycle, a key type a key management service can hold, a registered provider, a
    /// key identifier the stored reference can hold, an algorithm that resolves the key's size and curve, and a row of
    /// the provider's mapping for them. The key's public half is read from the service and checked against that row,
    /// and the binding is proved by a signature.
    /// </summary>
    /// <param name="request">The <see cref="ImportRemoteSecretRequest"/> to validate.</param>
    /// <param name="createdWhen">The instant the secret is created at, which its activation cannot precede.</param>
    /// <returns>The resolved size and curve of the key, or the error that describes the first failed
    /// check.</returns>
    SecretCreateValidation ValidateImportRemote(
        ImportRemoteSecretRequest request,
        DateTimeOffset createdWhen
    );

    /// <summary>
    /// Validates the metadata an <see cref="UpdateSecretRequest"/> declares for an existing key: the <c>use</c> is
    /// supported, and the <c>algorithm</c>, when set, agrees with the <c>use</c> and accepts the key. An asymmetric key
    /// is checked by its stored public half, which shows its type, size and curve, and fails the check when that half
    /// cannot be read; a symmetric key is checked by its type and size.
    /// </summary>
    /// <param name="secret">The <see cref="PersistedSecret"/> the update applies to.</param>
    /// <param name="request">The <see cref="UpdateSecretRequest"/> to validate.</param>
    /// <returns>A <see cref="ManagementError"/> describing the first failed check, or <c>null</c> when valid.</returns>
    ManagementError? ValidateUpdate(PersistedSecret secret, UpdateSecretRequest request);
}
