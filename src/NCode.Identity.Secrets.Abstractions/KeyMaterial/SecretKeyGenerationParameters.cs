// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.Secrets.KeyMaterial;

/// <summary>
/// Describes the parameters for generating a new secret key. Required inputs are supplied at construction; optional,
/// key-type-specific inputs (such as <see cref="Curve"/> for elliptic-curve keys) are set via init-only properties
/// so the shape can grow without breaking callers.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class SecretKeyGenerationParameters
{
    /// <summary>
    /// Gets the requested key size, in bits. When <see cref="Curve"/> is specified, it must be the size of the keys on
    /// that curve, and the key type refuses any other size.
    /// </summary>
    public required int KeySizeBits { get; init; }

    /// <summary>
    /// Gets the requested curve (the <c>JWK</c> <c>crv</c> name, for example <c>P-256</c>, <c>secp256k1</c> or
    /// <c>Ed25519</c>), or the parameter set of a key type that names one in its place (for example <c>ML-DSA-65</c>
    /// for an <c>AKP</c> key), or <c>null</c> to leave it to the key type: an <c>ecc</c> key then takes the one curve
    /// of its catalog whose keys are <see cref="KeySizeBits"/>, and is refused when several catalog curves have that
    /// size, and an <c>okp</c> key, whose signing and agreement curves can share a size, is refused. A key type without
    /// curves refuses a curve.
    /// </summary>
    public string? Curve { get; init; }
}
