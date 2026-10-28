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

namespace NCode.Identity.OpenId.Management.Auditing;

/// <summary>
/// Defines the catalogued event names (<c>event_name</c>) of the management API's grant and session audit events.
/// </summary>
/// <remarks>
/// This is the set of names the management API emits today, not a closed list: an extension names its own audit events
/// without editing this class. The names of resource-change events are built from
/// <see cref="ManagementResourceTypes"/> and the change type.
/// </remarks>
[PublicAPI]
public static class ManagementAuditEventNames
{
    /// <summary>
    /// An administrator revoked a single grant.
    /// </summary>
    public const string GrantRevoked = "grant.revoked";

    /// <summary>
    /// An administrator bulk-revoked grants by subject, client, and/or session filter.
    /// </summary>
    public const string GrantsRevokedBulk = "grant.revoked.bulk";

    /// <summary>
    /// An administrator revoked end-user sessions.
    /// </summary>
    public const string SessionsRevoked = "session.revoked.admin";
}
