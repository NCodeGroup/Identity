// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.OpenId.Authentication.Auditing;

/// <summary>
/// An audit event raised when a refresh token that a rotation spent is presented again within the client's
/// reuse interval and is answered with another refresh token of its rotation family, so an operator can
/// tell a client's retry from a rotation and from a reuse.
/// </summary>
[PublicAPI]
public sealed record RefreshTokenRetryAuditEvent : AuditEvent
{
    /// <inheritdoc />
    public override string EventName => OpenIdAuditEventNames.RefreshTokenRetry;

    /// <inheritdoc />
    public override string EventCategory => AuditEventCategories.Token;
}
