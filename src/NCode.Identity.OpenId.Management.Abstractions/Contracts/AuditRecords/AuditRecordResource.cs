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

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.AuditRecords;

/// <summary>
/// Represents an audit record: the envelope an operator filters on and the whole CloudTrail-shaped record (ADR-0283,
/// ADR-0303).
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class AuditRecordResource
{
    /// <summary>
    /// Gets the unique, time-ordered identifier of the audit event.
    /// </summary>
    public required Guid EventId { get; init; }

    /// <summary>
    /// Gets the time at which the audited operation occurred.
    /// </summary>
    public required DateTimeOffset EventTime { get; init; }

    /// <summary>
    /// Gets the dotted name of the audited operation, such as <c>token.issued</c>.
    /// </summary>
    public required string EventName { get; init; }

    /// <summary>
    /// Gets the category of the audited operation.
    /// </summary>
    public required string EventCategory { get; init; }

    /// <summary>
    /// Gets the outcome of the audited operation.
    /// </summary>
    public required string Outcome { get; init; }

    /// <summary>
    /// Gets the severity of the audited operation.
    /// </summary>
    public required string Severity { get; init; }

    /// <summary>
    /// Gets the identifier of the subject the audited operation acted on behalf of, or <c>null</c>.
    /// </summary>
    public string? SubjectId { get; init; }

    /// <summary>
    /// Gets the identifier of the client involved in the audited operation, or <c>null</c>.
    /// </summary>
    public string? ClientId { get; init; }

    /// <summary>
    /// Gets the type of the caller that performed the audited operation.
    /// </summary>
    public required string ActorType { get; init; }

    /// <summary>
    /// Gets the identifier of the caller that performed the audited operation, or <c>null</c>.
    /// </summary>
    public string? ActorId { get; init; }

    /// <summary>
    /// Gets the trace identifier of the request the audited operation was part of, or <c>null</c>.
    /// </summary>
    public string? TraceId { get; init; }

    /// <summary>
    /// Gets the IP address the request came from, or <c>null</c>.
    /// </summary>
    public string? SourceIp { get; init; }

    /// <summary>
    /// Gets the entities the audited operation acted on, the target first.
    /// </summary>
    public required IReadOnlyList<AuditRecordResourceReference> Resources { get; init; }

    /// <summary>
    /// Gets the whole audit record, which also carries the members the envelope does not, such as the request and the
    /// error.
    /// </summary>
    public required JsonElement Record { get; init; }
}
