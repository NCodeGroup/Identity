// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.RateLimiting;

/// <summary>
/// Carries what a <see cref="RateLimitDescriptor"/> needs to decide whether its limit applies to a request and which
/// partition of the limit the request counts in.
/// </summary>
// See ADR-1246.
[PublicAPI]
public readonly record struct RateLimitContext
{
    /// <summary>
    /// Gets the <see cref="OpenIdContext"/> of the request.
    /// </summary>
    public required OpenIdContext OpenIdContext { get; init; }

    /// <summary>
    /// Gets the confidential <see cref="OpenIdClient"/> the request authenticated as, or <see langword="null"/> for a
    /// stage that is judged before a client authenticates.
    /// </summary>
    public OpenIdClient? Client { get; init; }
}
