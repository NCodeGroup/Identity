// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using JetBrains.Annotations;

namespace NCode.Identity.Events.Audit;

/// <summary>
/// An <see cref="IAuditEvent"/> that records a create, update, or delete to a managed resource. The
/// common shape lets cross-cutting handlers observe every resource change through a single contract,
/// while concrete events remain strongly typed per resource kind.
/// </summary>
/// <remarks>
/// <see cref="ResourceValues"/> carries a non-sensitive snapshot of the resource and must never include
/// secret material.
/// </remarks>
[PublicAPI]
public interface IResourceChangeAuditEvent : IAuditEvent
{
    /// <summary>
    /// Gets the kind of resource that changed (for example, <c>client</c> or <c>client.secret</c>).
    /// </summary>
    string ResourceType { get; }

    /// <summary>
    /// Gets the identifier of the resource that changed, if known.
    /// </summary>
    string? ResourceId { get; }

    /// <summary>
    /// Gets the lifecycle transition that produced this event; one of the
    /// <see cref="ResourceChangeTypes"/> values.
    /// </summary>
    string ChangeType { get; }

    /// <summary>
    /// Gets a non-sensitive snapshot of the resource's values at the time of the change, or <c>null</c>
    /// when no snapshot is captured. Never contains secret material.
    /// </summary>
    JsonElement? ResourceValues { get; }
}
