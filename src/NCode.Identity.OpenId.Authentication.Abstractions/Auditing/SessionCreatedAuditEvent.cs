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

using JetBrains.Annotations;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.OpenId.Authentication.Auditing;

/// <summary>
/// An audit event raised when a server-side end-user session is established at sign-in.
/// </summary>
[PublicAPI]
public sealed record SessionCreatedAuditEvent : AuditEvent
{
    /// <inheritdoc />
    public override string EventName => OpenIdAuditEventNames.SessionCreated;

    /// <inheritdoc />
    public override string EventCategory => AuditEventCategories.Session;

    /// <summary>
    /// Gets the opaque session identifier (<c>sid</c>) that was created.
    /// </summary>
    public required string SessionId { get; init; }

    /// <inheritdoc />
    /// <remarks>
    /// Lists the created session as the event's only resource.
    /// </remarks>
    public override IReadOnlyList<AuditResource> Resources =>
        [new AuditResource(AuditResourceTypes.Session, SessionId)];
}
