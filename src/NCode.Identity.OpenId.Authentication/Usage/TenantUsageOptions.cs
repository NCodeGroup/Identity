// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Authentication.Usage;

/// <summary>
/// Contains the options for recording tenant usage into the durable daily ledger.
/// </summary>
// See ADR-1199 and ADR-1221.
public sealed class TenantUsageOptions
{
    /// <summary>
    /// Gets the name of the subsection of the OpenID configuration section that binds these options.
    /// </summary>
    public const string SectionName = "TenantUsage";

    /// <summary>
    /// Gets or sets how often each replica adds the usage it recorded to the ledger. A replica that stops abruptly loses
    /// at most this much of its usage. The default is one minute. It is at least one millisecond and at most about 49.7
    /// days, the periods a timer accepts.
    /// </summary>
    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets how long the ledger keeps a day's usage. The default is 400 days: a year of history to bill and
    /// report from, and a month more to settle a dispute over the oldest bill.
    /// </summary>
    public TimeSpan Retention { get; set; } = TimeSpan.FromDays(400);

    /// <summary>
    /// Gets or sets how often each replica removes the days the ledger no longer keeps. The default is one hour. It is
    /// at least one millisecond and at most about 49.7 days, the periods a timer accepts.
    /// </summary>
    public TimeSpan PruneInterval { get; set; } = TimeSpan.FromHours(1);
}
