// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
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
    /// Gets the size, in bits, of the key material to generate. For symmetric secrets this is the size of the
    /// random key; for <c>RSA</c> the modulus size; for <c>ECC</c> the curve size (advisory when
    /// <see cref="Curve"/> is specified).
    /// </summary>
    public required int KeySizeBits { get; init; }

    /// <summary>
    /// Gets the requested elliptic-curve identifier (the <c>JWK</c> <c>crv</c> name, for example <c>P-256</c>), or
    /// <c>null</c> to select a curve by <see cref="KeySizeBits"/>. Only applies to <c>ECC</c> secrets.
    /// </summary>
    public string? Curve { get; init; }

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
