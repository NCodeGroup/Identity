// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.OpenId.Authentication.Auditing;

/// <summary>
/// An audit event raised when a refresh token is presented whose grant has already been revoked
/// (typically rotated away). Presenting a revoked refresh token is a recognized token-theft signal and
/// is distinct from a benign expiry.
/// </summary>
[PublicAPI]
public sealed record RefreshTokenReplayAuditEvent : AuditEvent
{
    /// <inheritdoc />
    public override string EventName => OpenIdAuditEventNames.RefreshTokenReplay;

    /// <inheritdoc />
    public override string EventCategory => AuditEventCategories.Token;

    /// <inheritdoc />
    /// <remarks>
    /// A replayed refresh token is a recognized token-theft signal, so the event is
    /// <see cref="AuditSeverities.Critical"/>.
    /// </remarks>
    public override string Severity => AuditSeverities.Critical;
}
