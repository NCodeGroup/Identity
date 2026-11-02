// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.RateLimiting;

/// <summary>
/// Defines the <see cref="RateLimitDescriptor.Name"/> of each rate limit and quota the server registers. The name is
/// the partition of the limit's counters and the <c>partition</c> attribute of the rate-limiting metric.
/// </summary>
// See ADR-1246.
[PublicAPI]
public static class RateLimitPartitions
{
    /// <summary>
    /// The requests from one remote address to a tenant's protocol endpoints.
    /// </summary>
    public const string Address = "address";

    /// <summary>
    /// The requests from every caller to a tenant's protocol endpoints.
    /// </summary>
    public const string Tenant = "tenant";

    /// <summary>
    /// The requests from one authenticated client to a tenant's protocol endpoints.
    /// </summary>
    public const string Client = "client";

    /// <summary>
    /// The metered requests to a tenant's protocol endpoints in one UTC calendar day.
    /// </summary>
    public const string DailyRequests = "daily_requests";

    /// <summary>
    /// The token requests from a tenant's confidential clients in one UTC calendar day.
    /// </summary>
    public const string DailyTokenRequests = "daily_token_requests";
}
