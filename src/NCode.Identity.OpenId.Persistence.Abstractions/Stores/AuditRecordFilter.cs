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

namespace NCode.Identity.OpenId.Persistence.Stores;

/// <summary>
/// Describes which of a tenant's audit records a lookup returns: every criterion that is set must match, as CloudTrail's
/// <c>LookupEvents</c> filters do.
/// </summary>
// See ADR-0303.
[PublicAPI]
public readonly record struct AuditRecordFilter
{
    /// <summary>
    /// Gets the identifier of the tenant whose records are returned. A lookup never crosses tenants.
    /// </summary>
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets the earliest event time returned, inclusive, or <see langword="null"/> for no lower bound.
    /// </summary>
    public DateTimeOffset? StartTime { get; init; }

    /// <summary>
    /// Gets the latest event time returned, exclusive, or <see langword="null"/> for no upper bound.
    /// </summary>
    public DateTimeOffset? EndTime { get; init; }

    /// <summary>
    /// Gets the event name the records must have, or <see langword="null"/>.
    /// </summary>
    public string? EventName { get; init; }

    /// <summary>
    /// Gets the event category the records must have, or <see langword="null"/>.
    /// </summary>
    public string? EventCategory { get; init; }

    /// <summary>
    /// Gets the outcome the records must have, or <see langword="null"/>.
    /// </summary>
    public string? Outcome { get; init; }

    /// <summary>
    /// Gets the subject the records must name, or <see langword="null"/>.
    /// </summary>
    public string? SubjectId { get; init; }

    /// <summary>
    /// Gets the client the records must name, or <see langword="null"/>.
    /// </summary>
    public string? ClientId { get; init; }

    /// <summary>
    /// Gets the identifier of the actor the records must name, or <see langword="null"/>.
    /// </summary>
    public string? ActorId { get; init; }

    /// <summary>
    /// Gets the type of a resource the records must name, or <see langword="null"/>.
    /// </summary>
    public string? ResourceType { get; init; }

    /// <summary>
    /// Gets the identifier of a resource the records must name, or <see langword="null"/>. With
    /// <see cref="ResourceType"/>, the same resource must have both.
    /// </summary>
    public string? ResourceId { get; init; }

    /// <summary>
    /// Gets the trace identifier the records must carry, or <see langword="null"/>.
    /// </summary>
    public string? TraceId { get; init; }
}
