// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Capabilities;

/// <summary>
/// Describes a single secret type the server understands and the capabilities it exposes, so that management
/// clients can drive their UI (for example which types can be created, or which expose an exportable public key)
/// from server discovery rather than hard-coded assumptions.
/// </summary>
// See ADR-1067.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class SecretTypeCapabilityResource
{
    /// <summary>
    /// Gets the secret-type discriminator (for example <c>symmetric</c>, <c>rsa</c>, or <c>ecc</c>).
    /// </summary>
    public required string SecretType { get; init; }

    /// <summary>
    /// Gets a value indicating whether this secret type uses asymmetric cryptography.
    /// </summary>
    public required bool IsAsymmetric { get; init; }

    /// <summary>
    /// Gets a value indicating whether secrets of this type expose exportable public-key material (JWK and PEM).
    /// </summary>
    public required bool SupportsPublicKeyExport { get; init; }

    /// <summary>
    /// Gets a value indicating whether secrets of this type support importing caller-supplied public-key material.
    /// </summary>
    public required bool SupportsPublicKeyImport { get; init; }
}
