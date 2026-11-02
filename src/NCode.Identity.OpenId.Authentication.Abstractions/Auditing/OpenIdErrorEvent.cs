// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.OpenId.Authentication.Auditing;

/// <summary>
/// An audit event raised when an <c>OAuth</c> or <c>OpenID Connect</c> protocol endpoint returns an
/// error response, whether the error was thrown or returned directly. The event is published once per
/// errored request from the single endpoint-filter funnel, so no publishing site is required at the
/// individual error call sites. The error code, description and HTTP status code are carried by
/// <see cref="AuditEvent.Error"/>.
/// </summary>
[PublicAPI]
public sealed record OpenIdErrorEvent : AuditEvent
{
    /// <inheritdoc />
    public override string EventName => OpenIdAuditEventNames.Error;

    /// <inheritdoc />
    public override string EventCategory => AuditEventCategories.Protocol;
}
