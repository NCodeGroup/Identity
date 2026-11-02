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
    /// Gets the type of secret to generate. Supported values are <c>symmetric</c>, <c>rsa</c>, and <c>ecc</c>.
    /// </summary>
    public required string SecretType { get; init; }

    /// <summary>
    /// Gets the size, in bits, of the key material to generate.
    /// </summary>
    public required int KeySizeBits { get; init; }

    /// <summary>
    /// Gets the requested elliptic-curve identifier (the <c>JWK</c> <c>crv</c> name, for example <c>P-256</c>), or
    /// <c>null</c> to select a curve by <see cref="KeySizeBits"/>. Only applies to <c>ecc</c> secrets.
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
