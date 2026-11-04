// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Jose.SdJwt.Options;

/// <summary>
/// The limits and parameters of the Selective Disclosure JWT (SD-JWT) services, validated when the host starts.
/// </summary>
// See ADR-1405.
[PublicAPI]
public sealed class SdJwtOptions
{
    /// <summary>
    /// The smallest salt that RFC 9901 §4.2.1 permits, 128 bits.
    /// </summary>
    public const int MinimumSaltSizeBytes = 16;

    /// <summary>
    /// Gets or sets the size, in bytes, of the salt of each disclosure the issuer creates. The salt comes from a
    /// cryptographically secure random number generator. The default value, and the smallest permitted, is
    /// <see cref="MinimumSaltSizeBytes"/>.
    /// </summary>
    public int SaltSizeBytes { get; set; } = MinimumSaltSizeBytes;

    /// <summary>
    /// Gets or sets the largest number of disclosures an SD-JWT may carry, as issued or as presented; a presentation
    /// with more is refused before any disclosure is decoded. The default value is 256.
    /// </summary>
    public int MaxDisclosureCount { get; set; } = 256;

    /// <summary>
    /// Gets or sets the deepest nesting of objects and arrays the claims of an SD-JWT may reach once its disclosures
    /// are resolved; a presentation that nests deeper is refused. The default value is 32.
    /// </summary>
    public int MaxNestingDepth { get; set; } = 32;
}
