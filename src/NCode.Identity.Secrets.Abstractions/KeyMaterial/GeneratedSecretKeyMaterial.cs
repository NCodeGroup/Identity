// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Secrets.KeyTypes;

namespace NCode.Identity.Secrets.KeyMaterial;

/// <summary>
/// Represents freshly-generated key material produced by a <see cref="ISecretKeyTypeDescriptor"/>, with the
/// private key already protected via the supplied <see cref="SecretKeyMaterialProtector"/>.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class GeneratedSecretKeyMaterial
{
    /// <summary>
    /// Gets the opaque protected representation of the private key, as produced by the
    /// <see cref="SecretKeyMaterialProtector"/>.
    /// </summary>
    public required string ProtectedKeyMaterial { get; init; }

    /// <summary>
    /// Gets the actual size, in bits, of the generated key material (which may be normalized from the request).
    /// </summary>
    public required int KeySizeBits { get; init; }

    /// <summary>
    /// Gets the encoding of the stored material. See <see cref="SecretEncodings"/> for possible values.
    /// </summary>
    // See ADR-1104.
    public required string SecretEncoding { get; init; }

    /// <summary>
    /// Gets the one-time unprotected key material, base64url-encoded, or <c>null</c> when the key has no
    /// externally-held material to reveal. Populated only for symmetric keys; asymmetric private keys are never
    /// surfaced.
    /// </summary>
    // See ADR-1050.
    public required string? SecretMaterial { get; init; }

    /// <summary>
    /// Gets the base64url-encoded <c>SubjectPublicKeyInfo</c> DER of the generated key's public half, or
    /// <c>null</c> when the key has no public half (a symmetric key).
    /// </summary>
    // See ADR-1268.
    public required string? PublicKey { get; init; }
}
