// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.Secrets.Persistence;

/// <summary>
/// Contains the metadata and caller-supplied <b>public</b> key material used to import a new
/// <see cref="PersistedSecret"/>. Only public asymmetric material is accepted; private or symmetric material is
/// rejected by the resolved <c>ISecretKeyTypeDescriptor</c>.
/// </summary>
// See ADR-1104.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class SecretKeyImportRequest
{
    /// <summary>
    /// Gets the natural identifier to assign to the imported secret.
    /// </summary>
    public required string SecretId { get; init; }

    /// <summary>
    /// Gets the key identifier (<c>kid</c>) to select the imported key by, or <c>null</c> to identify the key by its
    /// <see cref="SecretId"/>.
    /// </summary>
    public string? KeyId { get; init; }

    /// <summary>
    /// Gets the secret type (crypto family) to import the material as, which selects the
    /// <c>ISecretKeyTypeDescriptor</c> that parses it. See <c>SecretKeyTypes</c>.
    /// </summary>
    public required string SecretType { get; init; }

    /// <summary>
    /// Gets the name of the encoding of the supplied <see cref="KeyMaterial"/>, such as a
    /// <see cref="SecretKeyImportFormats"/> name. It must be one of the formats the secret type imports.
    /// </summary>
    public required string Format { get; init; }

    /// <summary>
    /// Gets the caller-supplied public key material, encoded per <see cref="Format"/> (JWK JSON text, PEM text, or
    /// base64 DER for a certificate).
    /// </summary>
    public required string KeyMaterial { get; init; }

    /// <summary>
    /// Gets the intended use for the imported secret, or <c>null</c> for any compatible use.
    /// See <c>SecretKeyUses</c> for possible values.
    /// </summary>
    public required string? Use { get; init; }

    /// <summary>
    /// Gets the intended algorithm for the imported secret, or <c>null</c> for any compatible algorithm.
    /// </summary>
    public required string? Algorithm { get; init; }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> when the imported secret is considered created.
    /// </summary>
    public required DateTimeOffset CreatedWhen { get; init; }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> when the imported secret expires and is no longer valid.
    /// </summary>
    public required DateTimeOffset ExpiresWhen { get; init; }
}
