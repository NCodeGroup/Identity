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
