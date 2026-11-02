// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.RateLimiting;

/// <summary>
/// The outcome of judging a request against the rate limits of a stage: admitted, or refused until
/// <see cref="RetryAfter"/> has elapsed. The default value admits the request.
/// </summary>
/// <param name="RetryAfter">How long the caller waits before retrying; <see langword="null"/> when admitted.</param>
// See ADR-1196 and ADR-1246.
[PublicAPI]
public readonly record struct RateLimitDecision(TimeSpan? RetryAfter)
{
    /// <summary>
    /// Gets the decision that admits a request.
    /// </summary>
    public static RateLimitDecision Admitted => default;

    /// <summary>
    /// Gets a value indicating whether the request is refused.
    /// </summary>
    public bool IsRejected => RetryAfter.HasValue;
}
