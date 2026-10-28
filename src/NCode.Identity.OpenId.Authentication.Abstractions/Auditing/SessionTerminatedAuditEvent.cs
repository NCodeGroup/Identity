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
using NCode.Identity.OpenId.Authentication.Sessions;

namespace NCode.Identity.OpenId.Authentication.Auditing;

/// <summary>
/// An audit event raised when a server-side end-user session (or every session a subject holds) is terminated.
/// </summary>
[PublicAPI]
public sealed record SessionTerminatedAuditEvent : AuditEvent
{
    /// <inheritdoc />
    public override string Action => OpenIdAuditEventNames.SessionTerminated;

    /// <summary>
    /// Gets the opaque session identifier (<c>sid</c>) that was terminated, or <see langword="null"/> for a
    /// subject-wide termination.
    /// </summary>
    public required string? SessionId { get; init; }

    /// <summary>
    /// Gets the reason the session was terminated: one of <see cref="SessionTerminationReasons"/> or an extension's own
    /// name.
    /// </summary>
    public required string TerminationReason { get; init; }
}
