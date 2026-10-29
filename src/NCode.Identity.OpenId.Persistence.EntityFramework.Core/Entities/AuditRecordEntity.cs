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

using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.EntityFramework.Configuration;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Core.Entities;

/// <summary>
/// Represents an entity framework data contract for an audit record: its indexed envelope and the whole record's JSON
/// (ADR-0283, ADR-0303). The tenant is referenced by identifier, not by a foreign key, so a record outlives a deleted
/// tenant until its retention ends. Each index leads with the field a lookup filters on and then the event time, so a
/// lookup reads one time range of one value.
/// </summary>
[Index(nameof(EventId), IsUnique = true)]
[Index(nameof(EventTime))]
[Index(nameof(TenantId), nameof(EventTime))]
[Index(nameof(EventName), nameof(EventTime))]
[Index(nameof(SubjectId), nameof(EventTime))]
[Index(nameof(ClientId), nameof(EventTime))]
[Index(nameof(ActorId), nameof(EventTime))]
[Index(nameof(TraceId))]
public sealed class AuditRecordEntity
{
    /// <summary>
    /// Gets the surrogate identifier for this entity.
    /// </summary>
    [Key]
    [UseIdGenerator]
    public long Id { get; init; }

    /// <summary>
    /// Gets the unique, time-ordered identifier of the audit event.
    /// </summary>
    public required Guid EventId { get; init; }

    /// <summary>
    /// Gets the time at which the audited operation occurred.
    /// </summary>
    public required DateTimeOffset EventTime { get; init; }

    /// <summary>
    /// Gets the dotted name of the audited operation.
    /// </summary>
    [Unicode(false)]
    [MaxLength(OpenIdMaxLengths.AuditName)]
    public required string EventName { get; init; }

    /// <summary>
    /// Gets the category of the audited operation.
    /// </summary>
    [Unicode(false)]
    [MaxLength(OpenIdMaxLengths.AuditName)]
    public required string EventCategory { get; init; }

    /// <summary>
    /// Gets the outcome of the audited operation.
    /// </summary>
    [Unicode(false)]
    [MaxLength(OpenIdMaxLengths.AuditName)]
    public required string Outcome { get; init; }

    /// <summary>
    /// Gets the severity of the audited operation.
    /// </summary>
    [Unicode(false)]
    [MaxLength(OpenIdMaxLengths.AuditName)]
    public required string Severity { get; init; }

    /// <summary>
    /// Gets the identifier of the tenant the audited operation belongs to, or <see langword="null"/>.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.ResourceId)]
    public string? TenantId { get; init; }

    /// <summary>
    /// Gets the identifier of the subject the audited operation acted on behalf of, or <see langword="null"/>.
    /// </summary>
    [MaxLength(OpenIdMaxLengths.SubjectId)]
    public string? SubjectId { get; init; }

    /// <summary>
    /// Gets the identifier of the client involved in the audited operation, or <see langword="null"/>.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.ResourceId)]
    public string? ClientId { get; init; }

    /// <summary>
    /// Gets the type of the caller that performed the audited operation.
    /// </summary>
    [Unicode(false)]
    [MaxLength(OpenIdMaxLengths.AuditName)]
    public required string ActorType { get; init; }

    /// <summary>
    /// Gets the identifier of the caller that performed the audited operation, or <see langword="null"/>.
    /// </summary>
    [MaxLength(MaxLengths.ResourceId)]
    public string? ActorId { get; init; }

    /// <summary>
    /// Gets the trace identifier of the request the audited operation was part of, or <see langword="null"/>.
    /// </summary>
    [Unicode(false)]
    [MaxLength(OpenIdMaxLengths.AuditTraceId)]
    public string? TraceId { get; init; }

    /// <summary>
    /// Gets the IP address the request came from, or <see langword="null"/>.
    /// </summary>
    [Unicode(false)]
    [MaxLength(OpenIdMaxLengths.AuditSourceIp)]
    public string? SourceIp { get; init; }

    /// <summary>
    /// Gets the whole audit record's JSON.
    /// </summary>
    public required string Record { get; init; }

    /// <summary>
    /// Gets the entities the audited operation acted on.
    /// </summary>
    public ICollection<AuditRecordResourceEntity> Resources { get; init; } = [];
}
