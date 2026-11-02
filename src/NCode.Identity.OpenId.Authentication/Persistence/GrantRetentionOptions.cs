// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Authentication.Persistence;

/// <summary>
/// Contains the options for removing the grants that ended from the store.
/// </summary>
public sealed class GrantRetentionOptions
{
    /// <summary>
    /// Gets the name of the subsection of the OpenID configuration section that binds these options.
    /// </summary>
    public const string SectionName = "GrantRetention";

    /// <summary>
    /// Gets or sets how long a grant is kept after it ends, that is after it expires or, if earlier, after it is
    /// revoked. A refresh token presented again within this time is still recognized as a reuse, and a replayed
    /// authorization code still revokes its session. The default is 14 days.
    /// </summary>
    public TimeSpan Retention { get; set; } = TimeSpan.FromDays(14);

    /// <summary>
    /// Gets or sets how often each replica removes the grants that ended before the retention. The default is one
    /// hour. It is at least one millisecond and at most about 49.7 days, the periods a timer accepts.
    /// </summary>
    public TimeSpan PruneInterval { get; set; } = TimeSpan.FromHours(1);
}
