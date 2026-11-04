// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Jose.SdJwt.Issuance;

/// <summary>
/// Supplies the random values that issuing a Selective Disclosure JWT needs: the salt of each disclosure and the
/// position of each decoy array element. The default implementation draws from the platform's cryptographically
/// secure random number generator; the seam exists so a test can observe the values.
/// </summary>
internal interface ISdJwtRandomSource
{
    /// <summary>
    /// Creates a new salt: the base64url encoding of as many random bytes as the options configure, at least 128 bits.
    /// </summary>
    /// <returns>The salt.</returns>
    string NextSalt();

    /// <summary>
    /// Picks a uniformly random index below the specified bound.
    /// </summary>
    /// <param name="maxExclusive">The exclusive upper bound, at least 1.</param>
    /// <returns>An index between zero, inclusive, and <paramref name="maxExclusive"/>, exclusive.</returns>
    int NextIndex(int maxExclusive);
}
