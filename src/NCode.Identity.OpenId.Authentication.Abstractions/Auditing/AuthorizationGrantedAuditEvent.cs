// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.OpenId.Authentication.Auditing;

/// <summary>
/// An audit event raised when an authorization request is granted for a subject.
/// </summary>
[PublicAPI]
public sealed record AuthorizationGrantedAuditEvent : AuditEvent
{
    /// <inheritdoc />
    public override string EventName => OpenIdAuditEventNames.AuthorizationGranted;

    /// <inheritdoc />
    public override string EventCategory => AuditEventCategories.Authorization;
}
