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
/// server/tenant/client; authorization and existence preconditions remain the responsibility of the
/// calling endpoint.
/// </remarks>
[PublicAPI]
public interface ISecretValidator
{
    /// <summary>
    /// Validates that the specified <see cref="CreateSecretRequest"/> describes a coherent secret, and resolves the
    /// parameters the key is generated with. A request that names an algorithm bound to curves, such as <c>ES256K</c>,
    /// and no curve resolves to the first curve the algorithm accepts that has the requested size, so the key is never
    /// generated on the curve the key type picks for the size; the caller generates the key with
    /// <see cref="SecretCreateValidation.GenerationParameters"/>.
    /// </summary>
    /// <param name="request">The <see cref="CreateSecretRequest"/> to validate.</param>
    /// <returns>The <see cref="SecretCreateValidation"/>: the generation parameters, or the first failed
    /// check.</returns>
    SecretCreateValidation ValidateCreate(CreateSecretRequest request);

    /// <summary>
    /// Validates that the specified <see cref="ImportSecretRequest"/> describes a coherent public-key import. The
    /// supplied key material itself is parsed and validated by the importer; this check asserts only the
    /// parameter-level coherence (recognized <c>format</c>, non-empty material, and supported <c>use</c>).
    /// </summary>
    /// <param name="request">The <see cref="ImportSecretRequest"/> to validate.</param>
    /// <returns>A <see cref="ManagementError"/> describing the first failed check, or <c>null</c> when valid.</returns>
    ManagementError? ValidateImport(ImportSecretRequest request);

    /// <summary>
    /// Validates the metadata an <see cref="UpdateSecretRequest"/> declares for an existing key: the <c>use</c> is
    /// supported, and the <c>algorithm</c>, when set, agrees with the <c>use</c> and accepts the key's type and size.
    /// </summary>
    /// <param name="secret">The <see cref="PersistedSecret"/> the update applies to.</param>
    /// <param name="request">The <see cref="UpdateSecretRequest"/> to validate.</param>
    /// <returns>A <see cref="ManagementError"/> describing the first failed check, or <c>null</c> when valid.</returns>
    ManagementError? ValidateUpdate(PersistedSecret secret, UpdateSecretRequest request);
}
