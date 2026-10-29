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
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Persistence.DataContracts;

/// <summary>
/// Contains an audit record as an audit store keeps it: the envelope fields an operator filters on, which the store
/// indexes, and the whole record as the JSON that <c>AuditRecordWriter</c> writes (ADR-0283, ADR-0303). An envelope value
/// longer than the store indexes is <see langword="null"/>, or for a required field its leading characters, while
/// <see cref="Record"/> keeps it whole.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class PersistedAuditRecord
{
    /// <summary>
    /// Gets the unique, time-ordered identifier of the audit event, a UUIDv7.
    /// </summary>
    public required Guid EventId { get; init; }

    /// <summary>
    /// Gets the time at which the audited operation occurred.
    /// </summary>
    public required DateTimeOffset EventTime { get; init; }

    /// <summary>
    /// Gets the dotted name of the audited operation, such as <c>token.issued</c>.
    /// </summary>
    [MaxLength(OpenIdMaxLengths.AuditName)]
    public required string EventName { get; init; }

    /// <summary>
    /// Gets the category of the audited operation, such as <c>token</c>.
    /// </summary>
    [MaxLength(OpenIdMaxLengths.AuditName)]
    public required string EventCategory { get; init; }

    /// <summary>
    /// Gets the outcome of the audited operation, such as <c>success</c>.
    /// </summary>
    [MaxLength(OpenIdMaxLengths.AuditName)]
    public required string Outcome { get; init; }

    /// <summary>
    /// Gets the severity of the audited operation, such as <c>info</c>.
    /// </summary>
    [MaxLength(OpenIdMaxLengths.AuditName)]
    public required string Severity { get; init; }

    /// <summary>
    /// Gets the identifier of the tenant the audited operation belongs to, or <see langword="null"/>.
    /// </summary>
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
    [MaxLength(MaxLengths.ResourceId)]
    public string? ClientId { get; init; }

    /// <summary>
    /// Gets the type of the caller that performed the audited operation, such as <c>user</c>.
    /// </summary>
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
    [MaxLength(OpenIdMaxLengths.AuditTraceId)]
    public string? TraceId { get; init; }

    /// <summary>
    /// Gets the IP address the request the audited operation was part of came from, or <see langword="null"/>.
    /// </summary>
    [MaxLength(OpenIdMaxLengths.AuditSourceIp)]
    public string? SourceIp { get; init; }

    /// <summary>
    /// Gets the entities the audited operation acted on, the target first.
    /// </summary>
    public required IReadOnlyList<PersistedAuditResource> Resources { get; init; }

    /// <summary>
    /// Gets the whole audit record, the JSON object that <c>AuditRecordWriter</c> writes.
    /// </summary>
    public required string Record { get; init; }
}
