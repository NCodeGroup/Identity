// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Events.Audit;

/// <summary>
/// Provides the canonical event category codes for an audit record.
/// </summary>
/// <remarks>
/// Event category is an extensible string vocabulary, not a closed enumeration, so a consumer or
/// sibling package can introduce additional categories without editing this package. Values are
/// lower-case and stable, suitable for use as a log, index or metric dimension.
/// </remarks>
[PublicAPI]
public static class AuditEventCategories
{
    /// <summary>
    /// The event category code for the authentication of a client or a subject.
    /// </summary>
    public const string Authentication = "authentication";

    /// <summary>
    /// The event category code for the authorization of a request and the consent behind it.
    /// </summary>
    public const string Authorization = "authorization";

    /// <summary>
    /// The event category code for the issuance, refresh and revocation of tokens.
    /// </summary>
    public const string Token = "token";

    /// <summary>
    /// The event category code for the lifecycle of a user session.
    /// </summary>
    public const string Session = "session";

    /// <summary>
    /// The event category code for protocol-level errors returned to a caller.
    /// </summary>
    public const string Protocol = "protocol";

    /// <summary>
    /// The event category code for changes made through the management plane.
    /// </summary>
    public const string Management = "management";
}
