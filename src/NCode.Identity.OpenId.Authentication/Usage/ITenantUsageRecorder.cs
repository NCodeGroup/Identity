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
    /// <param name="teamPath">The path of the teams the tenant belongs to, in the <see cref="TeamPaths"/> form, which
    /// the usage bills to. It is at most <see cref="OpenIdMaxLengths.TeamPath"/> characters.</param>
    /// <param name="tenantId">The identifier of the tenant.</param>
    /// <param name="meter">The name of the meter, such as <see cref="TenantUsageMeters.Requests"/>. Meters are an open
    /// vocabulary, so a package may record its own; a name is at most <see cref="OpenIdMaxLengths.UsageMeter"/>
    /// characters.</param>
    /// <exception cref="ArgumentException">The team path, tenant or meter is empty, or the team path or the meter's
    /// name is too long.</exception>
    // See ADR-1426.
    void Record(string teamPath, string tenantId, string meter);
}
