// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.OpenId.Management.Auditing;

/// <summary>
/// An audit event raised when an administrator grants or revokes a role. The principal holding the role is the
/// subject carried by <see cref="IAuditEvent.SubjectId"/>, the role assignment is the resource carried by
/// <see cref="IResourceChangeAuditEvent.ResourceId"/>, and the resource node the role is held at is the owning
/// resource named by <see cref="NodeType"/> and <see cref="NodeId"/>.
/// </summary>
// See ADR-1449.
[PublicAPI]
public sealed record RoleAssignmentChangedAuditEvent : ResourceChangeAuditEvent
{
    /// <summary>
    /// Gets the type of the resource node the role is held at, such as <c>team</c>.
    /// </summary>
    public required string NodeType { get; init; }

    /// <summary>
    /// Gets the identifier of the resource node the role is held at.
    /// </summary>
    public required string NodeId { get; init; }

    /// <inheritdoc />
    public override string ResourceType => ManagementResourceTypes.RoleAssignment;

    /// <inheritdoc />
    /// <remarks>
    /// Lists the resource node the role is held at.
    /// </remarks>
    protected override IEnumerable<AuditResource> ParentResources =>
        [new AuditResource(NodeType, NodeId)];
}
