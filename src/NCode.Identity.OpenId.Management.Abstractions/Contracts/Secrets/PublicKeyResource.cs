// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Management.Contracts.Secrets;

/// <summary>
/// Represents the REST resource returned when reading the <b>public</b> key material of an asymmetric secret. It
/// carries the public key in two representations: a <c>JSON Web Key (JWK)</c> for programmatic consumers and a
/// <c>SubjectPublicKeyInfo</c> <c>PEM</c> for human/tooling copy-paste. No private key material is ever included,
/// and this resource exists only for asymmetric secrets — a symmetric secret has no public key sub-resource.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class PublicKeyResource
{
    /// <inheritdoc cref="PersistedSecret.SecretId"/>
    public required string SecretId { get; init; }

    /// <inheritdoc cref="PersistedSecret.SecretType"/>
    public required string SecretType { get; init; }

    /// <summary>
    /// Gets the public key as a <c>JSON Web Key (JWK)</c> per <c>RFC 7517</c>, containing only the public members
    /// (for example <c>n</c>/<c>e</c> for <c>RSA</c> or <c>crv</c>/<c>x</c>/<c>y</c> for <c>EC</c>). This is the same
    /// representation the <c>JWKS</c> endpoint publishes for the key.
    /// </summary>
    public required JsonElement Jwk { get; init; }

    /// <summary>
    /// Gets the public key as a <c>SubjectPublicKeyInfo</c> <c>PEM</c> string (<c>-----BEGIN PUBLIC KEY-----</c>),
    /// the format accepted by most cryptographic tooling and libraries.
    /// </summary>
    public required string Pem { get; init; }
}
