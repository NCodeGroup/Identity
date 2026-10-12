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
/// An audit event raised when an administrator creates, updates, or deletes a scope belonging to a
/// resource server. The owning resource server is carried by <see cref="ResourceServerId"/>, the owning
/// tenant by <see cref="IAuditEvent.TenantId"/>, and the scope value by
/// <see cref="IResourceChangeAuditEvent.ResourceId"/>.
/// </summary>
[PublicAPI]
public sealed record ResourceServerScopeChangedAuditEvent : ResourceChangeAuditEvent
{
    /// <summary>
    /// Gets the identifier of the resource server the scope belongs to.
    /// </summary>
    public required string ResourceServerId { get; init; }

    /// <inheritdoc />
    public override string ResourceType => ManagementResourceTypes.ResourceServerScope;
}
