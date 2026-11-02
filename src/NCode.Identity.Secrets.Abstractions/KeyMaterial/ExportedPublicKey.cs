// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Jwk;
using NCode.Identity.Secrets.KeyTypes;

namespace NCode.Identity.Secrets.KeyMaterial;

/// <summary>
/// Represents the exported <b>public</b> material of an asymmetric secret key, produced by a
/// <see cref="ISecretKeyTypeDescriptor"/> — a <c>JSON Web Key</c> (RFC 7517) object and a
/// <c>SubjectPublicKeyInfo</c> PEM. No private key material is included.
/// </summary>
// See ADR-1104.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class ExportedPublicKey
{
    /// <summary>
    /// Gets the public key as a <c>JSON Web Key</c> (RFC 7517) carrying the key-type-specific members
    /// (for example <c>kty</c>/<c>n</c>/<c>e</c> or <c>kty</c>/<c>crv</c>/<c>x</c>/<c>y</c>) plus any
    /// <c>kid</c>/<c>use</c>/<c>alg</c> from the key's metadata.
    /// </summary>
    public required JsonWebKey Jwk { get; init; }

    /// <summary>
    /// Gets the public key as a <c>SubjectPublicKeyInfo</c> PEM (<c>-----BEGIN PUBLIC KEY-----</c>).
    /// </summary>
    public required string Pem { get; init; }
}
