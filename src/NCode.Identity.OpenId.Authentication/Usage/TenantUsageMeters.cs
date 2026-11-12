// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Authentication.Usage;

/// <summary>
/// Contains the names of the tenant-usage meters the family records. Meters are an open vocabulary: a
/// package records its own by name through <see cref="ITenantUsageRecorder"/>.
/// </summary>
// See ADR-1199 and ADR-1198.
public static class TenantUsageMeters
{
    /// <summary>
    /// Contains the name of the meter that counts the requests a tenant's protocol endpoints served: every request
    /// the rate limits and the daily request quota admitted.
    /// </summary>
    public const string Requests = "requests";

    /// <summary>
    /// Contains the name of the meter that counts the token requests of a tenant's confidential clients that the daily
    /// token-request quota admitted.
    /// </summary>
    public const string TokenRequests = "token_requests";

    /// <summary>
    /// Contains the name of the meter that counts the requests a tenant's protocol endpoints served past its daily
    /// request quota, the billable overage. Each is also counted by <see cref="Requests"/>.
    /// </summary>
    public const string RequestsOverage = "requests_overage";

    /// <summary>
    /// Contains the name of the meter that counts the token requests of a tenant's confidential clients served past its
    /// daily token-request quota, the billable overage. Each is also counted by <see cref="TokenRequests"/>.
    /// </summary>
    public const string TokenRequestsOverage = "token_requests_overage";
}
