// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Jwt;

/// <summary>
/// Provides the optional inputs of a lifetime validation that a test runs.
/// </summary>
public sealed class JwtTestLifetimeOptions
{
    /// <summary>
    /// Gets a value indicating whether the token must carry an expiration time.
    /// </summary>
    public bool RequireExpirationTime { get; init; } = true;

    /// <summary>
    /// Gets the maximum lifetime of the token, or <see langword="null"/> for none.
    /// </summary>
    public TimeSpan? MaxLifetime { get; init; }

    /// <summary>
    /// Gets a value indicating whether the token must carry a not-before time.
    /// </summary>
    public bool RequireNotBefore { get; init; }

    /// <summary>
    /// Gets a value indicating whether the token must carry an issue time.
    /// </summary>
    public bool RequireIssuedAt { get; init; }
}
