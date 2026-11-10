// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Jose.SdJwt.Profiles;

namespace NCode.Identity.Jose.SdJwt.Tests.Verification;

/// <summary>
/// The inputs a verifier test varies when it issues an SD-JWT.
/// </summary>
public sealed class SdJwtTestIssuanceOptions
{
    /// <summary>
    /// Gets the JSON claims to issue, or <see langword="null"/> for the test's default claims.
    /// </summary>
    public string? ClaimsJson { get; init; }

    /// <summary>
    /// Gets the number of decoy digests to add.
    /// </summary>
    public int DecoyDigestCount { get; init; }

    /// <summary>
    /// Gets the profile to issue under, or <see langword="null"/> for none.
    /// </summary>
    public SdJwtProfile? Profile { get; init; }

    /// <summary>
    /// Gets a value indicating whether the issuer JWT binds the holder's key.
    /// </summary>
    public bool WithHolderKey { get; init; } = true;
}
