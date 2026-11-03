// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Capabilities;

/// <summary>
/// Describes one secret type an algorithm accepts and the key sizes it accepts keys of that type in. An algorithm whose
/// name serves several key types, such as <c>ECDH-ES</c> over <c>EC</c> and <c>OKP</c> keys (RFC 8037 §3.2), lists one
/// per secret type, each with its own sizes.
/// </summary>
// See ADR-1389.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class AlgorithmKeyTypeCapabilityResource
{
    /// <summary>
    /// Gets the secret-type discriminator (for example <c>rsa</c> or <c>ecc</c>) whose key material is compatible with
    /// the algorithm.
    /// </summary>
    public required string SecretType { get; init; }

    /// <summary>
    /// Gets the legal key sizes, in bits, of keys of <see cref="SecretType"/> for the algorithm, as one or more
    /// inclusive ranges.
    /// </summary>
    public required IReadOnlyList<KeyBitSizesResource> KeyBitSizes { get; init; }

    /// <summary>
    /// Gets the <c>JWK</c> <c>crv</c> names of the curves that keys of <see cref="SecretType"/> must be on for the
    /// algorithm, in its order of preference (for example <c>secp256k1</c> for <c>ES256K</c>), or an empty list when
    /// the type has no curve. A key created without a curve is generated on the first of them that has its size.
    /// </summary>
    public IReadOnlyList<string> Curves { get; init; } = [];
}
