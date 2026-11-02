// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Secrets.KeyTypes;

namespace NCode.Identity.Secrets.KeyMaterial;

/// <summary>
/// Describes caller-supplied <b>public</b> key material to import into a new secret. The
/// <see cref="ISecretKeyTypeDescriptor"/> for the requested key type parses and validates the material.
/// </summary>
// See ADR-1104.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class SecretKeyImportParameters
{
    /// <summary>
    /// Gets the name of the encoding of the supplied <see cref="KeyMaterial"/>, such as a
    /// <see cref="SecretKeyImportFormats"/> name. Names compare ordinally.
    /// </summary>
    public required string Format { get; init; }

    /// <summary>
    /// Gets the caller-supplied public key material, encoded per <see cref="Format"/> (JWK JSON text, PEM text, or
    /// base64 DER for a certificate).
    /// </summary>
    public required string KeyMaterial { get; init; }
}
