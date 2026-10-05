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

using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using NCode.Identity.Events;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.OpenId.Management.Auditing;

/// <summary>
/// Provides the default implementation of <see cref="IManagementAuditRecorder"/> that derives the audit
/// envelope — including the acting administrator — from the current request and publishes through the
/// event seam.
/// </summary>
internal class DefaultManagementAuditRecorder(
    IEventPublisher eventPublisher,
    TimeProvider timeProvider
) : IManagementAuditRecorder
{
    private IEventPublisher EventPublisher { get; } = eventPublisher;
    private TimeProvider TimeProvider { get; } = timeProvider;

    /// <inheritdoc />
    public ValueTask RecordGrantRevokedAsync(
        HttpContext httpContext,
        string tenantId,
        string? subjectId,
        string? clientId,
        string? grantType,
        CancellationToken cancellationToken
    )
    {
        var auditEvent = new GrantRevokedAuditEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = TimeProvider.GetUtcNow(),
            CorrelationId = GetCorrelationId(httpContext),
            ActorId = GetActorId(httpContext),
            TenantId = tenantId,
            SubjectId = subjectId,
            ClientId = clientId,
            Outcome = AuditOutcome.Success,
            GrantType = grantType,
        };

        return EventPublisher.PublishAsync(auditEvent, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask RecordGrantsRevokedAsync(
        HttpContext httpContext,
        string tenantId,
        string? subjectId,
        string? clientId,
        long revokedCount,
        CancellationToken cancellationToken
    )
    {
        var auditEvent = new GrantsRevokedBulkAuditEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = TimeProvider.GetUtcNow(),
            CorrelationId = GetCorrelationId(httpContext),
            ActorId = GetActorId(httpContext),
            TenantId = tenantId,
            SubjectId = subjectId,
            ClientId = clientId,
            Outcome = AuditOutcome.Success,
            RevokedCount = revokedCount,
        };

        return EventPublisher.PublishAsync(auditEvent, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask RecordClientChangedAsync(
        HttpContext httpContext,
        string tenantId,
        string clientId,
        string changeType,
        JsonElement? resourceValues,
        CancellationToken cancellationToken
    )
    {
        var auditEvent = new ClientChangedAuditEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = TimeProvider.GetUtcNow(),
            CorrelationId = GetCorrelationId(httpContext),
            ActorId = GetActorId(httpContext),
            TenantId = tenantId,
            ClientId = clientId,
            Outcome = AuditOutcome.Success,
            ResourceId = clientId,
            ChangeType = changeType,
            ResourceValues = resourceValues,
        };

        return EventPublisher.PublishAsync(auditEvent, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask RecordClientSecretChangedAsync(
        HttpContext httpContext,
        string tenantId,
        string clientId,
        string secretId,
        string changeType,
        JsonElement? resourceValues,
        CancellationToken cancellationToken
    )
    {
        var auditEvent = new ClientSecretChangedAuditEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = TimeProvider.GetUtcNow(),
            CorrelationId = GetCorrelationId(httpContext),
            ActorId = GetActorId(httpContext),
            TenantId = tenantId,
            ClientId = clientId,
            Outcome = AuditOutcome.Success,
            ResourceId = secretId,
            ChangeType = changeType,
            ResourceValues = resourceValues,
        };

        return EventPublisher.PublishAsync(auditEvent, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask RecordTenantChangedAsync(
        HttpContext httpContext,
        string tenantId,
        string changeType,
        JsonElement? resourceValues,
        CancellationToken cancellationToken
    )
    {
        var auditEvent = new TenantChangedAuditEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = TimeProvider.GetUtcNow(),
            CorrelationId = GetCorrelationId(httpContext),
            ActorId = GetActorId(httpContext),
            TenantId = tenantId,
            Outcome = AuditOutcome.Success,
            ResourceId = tenantId,
            ChangeType = changeType,
            ResourceValues = resourceValues,
        };

        return EventPublisher.PublishAsync(auditEvent, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask RecordTenantSecretChangedAsync(
        HttpContext httpContext,
        string tenantId,
        string secretId,
        string changeType,
        JsonElement? resourceValues,
        CancellationToken cancellationToken
    )
    {
        var auditEvent = new TenantSecretChangedAuditEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = TimeProvider.GetUtcNow(),
            CorrelationId = GetCorrelationId(httpContext),
            ActorId = GetActorId(httpContext),
            TenantId = tenantId,
            Outcome = AuditOutcome.Success,
            ResourceId = secretId,
            ChangeType = changeType,
            ResourceValues = resourceValues,
        };

        return EventPublisher.PublishAsync(auditEvent, cancellationToken);
    }

    private static string? GetCorrelationId(HttpContext httpContext) =>
        Activity.Current?.Id ?? httpContext.TraceIdentifier;

    private static string? GetActorId(HttpContext httpContext) =>
        httpContext.User.FindFirstValue("sub")
        ?? httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
}
