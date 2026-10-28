#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.OpenId.Management.Auditing;

/// <summary>
/// An audit event raised when an administrator revokes end-user sessions — a single session by its <c>sid</c> or every
/// session a subject holds. It attributes the acting administrator; the per-session <c>session.terminated</c> events
/// (grant revocation, back-channel logout, and auditing) are raised independently by the session-termination seam.
/// </summary>
[PublicAPI]
public sealed record SessionsRevokedAuditEvent : AuditEvent, ISupportAuditDetails
{
    /// <inheritdoc />
    public override string EventName => ManagementAuditEventNames.SessionsRevoked;

    /// <inheritdoc />
    public override string EventCategory => AuditEventCategories.Management;

    /// <summary>
    /// Gets the opaque <c>sid</c> of the single session revoked, or <c>null</c> for a subject-wide revocation.
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// Gets the number of sessions that were revoked.
    /// </summary>
    public long RevokedCount { get; init; }

    /// <inheritdoc />
    /// <remarks>
    /// Lists the revoked session as the event's only resource, or none for a subject-wide revocation.
    /// </remarks>
    public override IReadOnlyList<AuditResource> Resources =>
        SessionId is null ? [] : [new AuditResource(AuditResourceTypes.Session, SessionId)];

    /// <inheritdoc />
    /// <remarks>
    /// Writes the <c>revoked_count</c>.
    /// </remarks>
    public void WriteAuditDetails(Utf8JsonWriter writer) =>
        writer.WriteNumber("revoked_count", RevokedCount);
}
