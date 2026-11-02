// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Authentication.Sessions;

/// <summary>
/// Contains the options for terminating the sessions that ended while no request used them.
/// </summary>
public sealed class EndedSessionTerminationOptions
{
    /// <summary>
    /// Gets the name of the subsection of the OpenID configuration section that binds these options.
    /// </summary>
    public const string SectionName = "EndedSessionTermination";

    /// <summary>
    /// Gets or sets how often one replica terminates the sessions that ended while no request used them, which
    /// revokes the grants issued under each and notifies its relying parties. The default is one minute. It is at
    /// least one millisecond and at most about 49.7 days, the periods a timer accepts.
    /// </summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(1);
}
