#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.RateLimiting;

/// <summary>
/// Defines the <see cref="RateLimitDescriptor.Name"/> of each rate limit and quota the server registers. The name is
/// the partition of the limit's counters and the <c>partition</c> attribute of the rate-limiting metric.
/// </summary>
// See ADR-0246.
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
