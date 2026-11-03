// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.Secrets.Persistence;

/// <summary>
/// Contains the metadata used to generate a new <see cref="PersistedSecret"/> whose key material is created
/// server-side and never supplied by the caller.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class GenerateSecretRequest
{
    /// <summary>
    /// Gets the natural identifier to assign to the generated secret.
    /// </summary>
    public required string SecretId { get; init; }

    /// <summary>
    /// Gets the type of secret to generate. See <see cref="SecretTypes"/> for the supported values.
    /// </summary>
    public required string SecretType { get; init; }

    /// <summary>
    /// Gets the parameters the key material is generated with: its size, in bits, and, for a key type with curves, the
    /// curve. For a shared secret the size is the entropy of the opaque string, and no curve applies.
    /// </summary>
    public required SecretKeyGenerationParameters GenerationParameters { get; init; }

    /// <summary>
    /// Gets the intended use for the generated secret, or <c>null</c> for any compatible use.
    /// See <c>SecretKeyUses</c> for possible values.
    /// </summary>
    public required string? Use { get; init; }

    /// <summary>
    /// Gets the intended algorithm for the generated secret, or <c>null</c> for any compatible algorithm.
    /// </summary>
    public required string? Algorithm { get; init; }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> when the generated secret is considered created.
    /// </summary>
    public required DateTimeOffset CreatedWhen { get; init; }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> when the generated secret expires and is no longer valid.
    /// </summary>
    public required DateTimeOffset ExpiresWhen { get; init; }
}
