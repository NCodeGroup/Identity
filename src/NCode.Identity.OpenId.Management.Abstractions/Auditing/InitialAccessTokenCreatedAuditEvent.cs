// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.OpenId.Management.Auditing;

/// <summary>
/// An audit event raised when an administrator mints an initial access token (RFC 7591) that authorizes protected
/// dynamic client registration. The token's secret material is never carried on the event.
/// </summary>
[PublicAPI]
public sealed record InitialAccessTokenCreatedAuditEvent : ResourceChangeAuditEvent
{
    /// <inheritdoc />
    public override string ResourceType => ManagementResourceTypes.InitialAccessToken;
}
