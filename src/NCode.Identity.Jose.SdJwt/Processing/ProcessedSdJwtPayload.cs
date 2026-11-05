// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json.Nodes;

namespace NCode.Identity.Jose.SdJwt.Processing;

/// <summary>
/// The outcome of resolving the disclosures of a Selective Disclosure JWT into its issuer-signed JWT payload: the
/// claims with every disclosed value in place and the <c>_sd</c>, <c>_sd_alg</c> and <c>...</c> members removed, and
/// where each disclosure landed.
/// </summary>
/// <param name="claims">The resolved claims.</param>
/// <param name="disclosed">Each presented disclosure, resolved to its place in <paramref name="claims"/>.</param>
internal sealed class ProcessedSdJwtPayload(
    JsonObject claims,
    IEnumerable<DisclosedClaim> disclosed
)
{
    /// <summary>
    /// Gets the resolved claims.
    /// </summary>
    public JsonObject Claims { get; } = claims;

    /// <summary>
    /// Gets each presented disclosure, resolved to its place in <see cref="Claims"/>.
    /// </summary>
    public ImmutableArray<DisclosedClaim> Disclosed { get; } = [.. disclosed];
}
