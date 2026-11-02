// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.OpenId.Authentication.Auditing;

/// <summary>
/// An audit event raised when a token (a refresh-token grant) is revoked.
/// </summary>
[PublicAPI]
public sealed record TokenRevokedAuditEvent : AuditEvent
{
    /// <inheritdoc />
    public override string EventName => OpenIdAuditEventNames.TokenRevoked;

    /// <inheritdoc />
    public override string EventCategory => AuditEventCategories.Token;
}
