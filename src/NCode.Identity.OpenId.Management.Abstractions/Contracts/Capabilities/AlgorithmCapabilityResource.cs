// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Capabilities;

/// <summary>
/// Describes a single signature or key-management algorithm the server supports, so management clients can drive
/// the create-secret form's algorithm, use, and key-size inputs from server discovery rather than hard-coded
/// defaults.
/// </summary>
// See ADR-1067.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class AlgorithmCapabilityResource
{
    /// <summary>
    /// Gets the algorithm's <c>JWA</c> code (for example <c>RS256</c> or <c>RSA-OAEP</c>).
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Gets the key use the algorithm implies (<c>sig</c> for signature algorithms, <c>enc</c> for key-management
    /// algorithms). See <see cref="NCode.Identity.Secrets.SecretKeyUses"/>.
    /// </summary>
    public required string Use { get; init; }

    /// <summary>
    /// Gets the secret types whose key material is compatible with this algorithm, each with the key sizes the
    /// algorithm accepts keys of that type in, in the algorithm's order of preference, so clients can pair the
    /// algorithm picker with the secret-type picker and bound the key size by the chosen type.
    /// </summary>
    // See ADR-1389.
    public required IReadOnlyList<AlgorithmKeyTypeCapabilityResource> KeyTypes { get; init; }
}
