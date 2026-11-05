// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Persistence;

namespace NCode.Identity.OpenId.Authentication.Usage;

/// <summary>
/// Records a tenant's usage into the durable daily ledger. Recording is in memory and never waits on the
/// database: each replica adds what it recorded to the ledger periodically.
/// </summary>
// See ADR-1199.
public interface ITenantUsageRecorder
{
    /// <summary>
    /// Records one unit of a tenant's usage of a meter on the current UTC calendar day.
    /// </summary>
    /// <param name="teamId">The identifier of the team the tenant belongs to, which the usage bills to.</param>
    /// <param name="tenantId">The identifier of the tenant.</param>
    /// <param name="meter">The name of the meter, such as <see cref="TenantUsageMeters.Requests"/>. Meters are an open
    /// vocabulary, so a package may record its own; a name is at most <see cref="OpenIdMaxLengths.UsageMeter"/>
    /// characters.</param>
    /// <exception cref="ArgumentException">The team, tenant or meter is empty, or the meter's name is too long.</exception>
    void Record(string teamId, string tenantId, string meter);
}
