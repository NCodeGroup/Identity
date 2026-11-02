// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;

namespace NCode.Identity.Bff.Sessions;

/// <summary>
/// The default <see cref="IBffSessionFeature"/>, which holds the session that a request resolved.
/// </summary>
/// <param name="metadata">The metadata of the resolved session.</param>
/// <param name="principal">The principal built from the resolved session.</param>
internal sealed class BffSessionFeature(BffSessionMetadata metadata, ClaimsPrincipal principal)
    : IBffSessionFeature
{
    /// <inheritdoc />
    public BffSessionMetadata Metadata { get; } = metadata;

    /// <inheritdoc />
    public ClaimsPrincipal Principal { get; } = principal;
}
