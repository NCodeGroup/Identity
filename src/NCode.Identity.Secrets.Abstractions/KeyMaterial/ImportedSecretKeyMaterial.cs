// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Secrets.KeyTypes;

namespace NCode.Identity.Secrets.KeyMaterial;

/// <summary>
/// Represents the result of importing caller-supplied <b>public</b> key material produced by a
/// <see cref="ISecretKeyTypeDescriptor"/>, with the public key already protected via the supplied
/// <see cref="SecretKeyMaterialProtector"/>.
/// </summary>
// See ADR-1104.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class ImportedSecretKeyMaterial
{
    /// <summary>
    /// Gets the opaque protected representation of the public key material, as produced by the
    /// <see cref="SecretKeyMaterialProtector"/>.
    /// </summary>
    public required string ProtectedKeyMaterial { get; init; }

    /// <summary>
    /// Gets the actual size, in bits, of the imported key material.
    /// </summary>
    public required int KeySizeBits { get; init; }

    /// <summary>
    /// Gets the encoding of the stored material. See <see cref="SecretEncodings"/> for possible values.
    /// </summary>
    public required string SecretEncoding { get; init; }
}
