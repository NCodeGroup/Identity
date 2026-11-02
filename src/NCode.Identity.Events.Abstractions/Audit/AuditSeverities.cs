// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Events.Audit;

/// <summary>
/// Provides the canonical severity codes for an audit record.
/// </summary>
/// <remarks>
/// Severity is an extensible string vocabulary, not a closed enumeration, so a consumer or sibling
/// package can introduce additional severities without a breaking change. Values are lower-case and
/// stable, suitable for use as a log or index dimension.
/// </remarks>
[PublicAPI]
public static class AuditSeverities
{
    /// <summary>
    /// The severity code for a routine audited operation that needs no attention.
    /// </summary>
    public const string Info = "info";

    /// <summary>
    /// The severity code for an audited operation that failed or was refused and may warrant review.
    /// </summary>
    public const string Warning = "warning";

    /// <summary>
    /// The severity code for an audited operation that indicates a likely security incident (for
    /// example the replay of a refresh token).
    /// </summary>
    public const string Critical = "critical";
}
