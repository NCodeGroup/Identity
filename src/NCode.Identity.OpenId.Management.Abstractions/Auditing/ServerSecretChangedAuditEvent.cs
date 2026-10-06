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

namespace NCode.Identity.OpenId.Management.Auditing;

/// <summary>
/// An audit event raised when an administrator creates, updates, or deletes a secret belonging to an
/// OpenID server. The owning server is carried by <see cref="ServerId"/> and the secret's identifier by
/// <see cref="IResourceChangeAuditEvent.ResourceId"/>; the event never carries secret material.
/// </summary>
/// <remarks>
/// Servers are the <c>GlobalAdmin</c>-only control plane and have no owning tenant, so
/// <see cref="IAuditEvent.TenantId"/> is not populated for this event.
/// </remarks>
[PublicAPI]
public sealed record ServerSecretChangedAuditEvent : ResourceChangeAuditEvent
{
    /// <summary>
    /// Gets the identifier of the server the secret belongs to.
    /// </summary>
    public required string ServerId { get; init; }

    /// <inheritdoc />
    public override string ResourceType => ManagementResourceTypes.ServerSecret;
}
