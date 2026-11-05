// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Authentication.Usage;

/// <summary>
/// Identifies a tenant's usage of one meter on one UTC calendar day.
/// </summary>
/// <param name="TeamId">The identifier of the team the tenant belongs to, which the usage bills to.</param>
/// <param name="TenantId">The identifier of the tenant.</param>
/// <param name="Day">The UTC calendar day.</param>
/// <param name="Meter">The name of the meter.</param>
internal readonly record struct TenantUsageKey(
    string TeamId,
    string TenantId,
    DateOnly Day,
    string Meter
);
