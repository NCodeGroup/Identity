// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Events.Audit;

/// <summary>
/// Provides the canonical event source codes for an audit record.
/// </summary>
/// <remarks>
/// Event source is an extensible string vocabulary, not a closed enumeration, so a consumer or sibling
/// package can introduce additional sources (for example another plane or service) without a
/// breaking change. Values are lower-case and stable, suitable for use as a log or index dimension.
/// </remarks>
[PublicAPI]
public static class AuditEventSources
{
    /// <summary>
    /// The event source code for the OpenID protocol endpoints (authorization, token, and so on).
    /// </summary>
    public const string OpenId = "openid";

    /// <summary>
    /// The event source code for the management API.
    /// </summary>
    public const string Management = "management";
}
