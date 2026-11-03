// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Secrets;

/// <summary>
/// Represents the request body to create a new server secret. The key material is generated server-side; the
/// caller never supplies it.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class CreateSecretRequest
{
    /// <summary>
    /// Gets the type of secret to generate: one of the secret types the server registers and its capabilities list,
    /// such as <c>symmetric</c>, <c>rsa</c>, <c>ecc</c> or <c>okp</c>.
    /// </summary>
    public required string SecretType { get; init; }

    /// <summary>
    /// Gets the size, in bits, of the key material to generate. With a <see cref="Curve"/>, it is the size of the keys
    /// on that curve.
    /// </summary>
    public required int KeySizeBits { get; init; }

    /// <summary>
    /// Gets the curve to generate the key on (the <c>JWK</c> <c>crv</c> name, for example <c>P-256</c> or
    /// <c>Ed25519</c>) for a secret type with curves, or <c>null</c> to leave it to the type: <c>ecc</c> then selects a
    /// curve by <see cref="KeySizeBits"/>, while <c>okp</c> requires one. With an <see cref="Algorithm"/> whose curves
    /// include two of the requested size, a curve is required. A curve whose keys are not <see cref="KeySizeBits"/> in
    /// size is refused, and a secret type without curves rejects a curve.
    /// </summary>
    public string? Curve { get; init; }

    /// <summary>
    /// Gets the intended use for the secret, or <c>null</c> for any compatible use.
    /// </summary>
    public string? Use { get; init; }

    /// <summary>
    /// Gets the intended algorithm for the secret, or <c>null</c> for any compatible algorithm.
    /// </summary>
    public string? Algorithm { get; init; }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> when the secret expires and is no longer valid.
    /// </summary>
    public required DateTimeOffset ExpiresWhen { get; init; }
}
