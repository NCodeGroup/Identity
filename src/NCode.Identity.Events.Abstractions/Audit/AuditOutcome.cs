// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Events.Audit;

/// <summary>
/// Provides the canonical outcome codes for an <see cref="IAuditEvent.Outcome"/>.
/// </summary>
/// <remarks>
/// Outcome is an extensible string vocabulary, not a closed enumeration, so a consumer or sibling
/// package can introduce additional outcomes (for example <c>denied</c>, <c>error</c>, or
/// <c>challenge</c>) without editing this package. Values are lower-case and stable, suitable for use as
/// a log or metric dimension.
/// </remarks>
[PublicAPI]
public static class AuditOutcome
{
    /// <summary>
    /// The outcome code indicating the audited operation completed successfully.
    /// </summary>
    public const string Success = "success";

    /// <summary>
    /// The outcome code indicating the audited operation failed.
    /// </summary>
    public const string Failure = "failure";

    /// <summary>
    /// The outcome code indicating the audited operation was denied (for example an authorization
    /// request that was refused, as opposed to an unexpected failure).
    /// </summary>
    public const string Denied = "denied";
}
