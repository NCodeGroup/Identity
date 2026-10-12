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
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NCode.Identity.Events;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Environments;

namespace NCode.Identity.OpenId.Management.Auditing;

/// <summary>
/// Provides the default implementation of <see cref="IManagementAuditRecorder"/> that derives the audit
/// envelope — including the acting administrator — from the current request and publishes through the
/// event seam.
/// </summary>
internal class DefaultManagementAuditRecorder(
    IEventPublisher eventPublisher,
    IOpenIdEnvironmentProvider environmentProvider,
    TimeProvider timeProvider
) : IManagementAuditRecorder
{
    private IEventPublisher EventPublisher { get; } = eventPublisher;
    private IOpenIdEnvironmentProvider EnvironmentProvider { get; } = environmentProvider;
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
            Source = GetSource(httpContext),
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
            Source = GetSource(httpContext),
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
            Source = GetSource(httpContext),
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
            Source = GetSource(httpContext),
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
            Source = GetSource(httpContext),
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
            Source = GetSource(httpContext),
            TenantId = tenantId,
            Outcome = AuditOutcome.Success,
            ResourceId = secretId,
            ChangeType = changeType,
            ResourceValues = resourceValues,
        };

        return EventPublisher.PublishAsync(auditEvent, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask RecordServerChangedAsync(
        HttpContext httpContext,
        string serverId,
        string changeType,
        JsonElement? resourceValues,
        CancellationToken cancellationToken
    )
    {
        var auditEvent = new ServerChangedAuditEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = TimeProvider.GetUtcNow(),
            CorrelationId = GetCorrelationId(httpContext),
            ActorId = GetActorId(httpContext),
            Source = GetSource(httpContext),
            Outcome = AuditOutcome.Success,
            ResourceId = serverId,
            ChangeType = changeType,
            ResourceValues = resourceValues,
        };

        return EventPublisher.PublishAsync(auditEvent, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask RecordServerSecretChangedAsync(
        HttpContext httpContext,
        string serverId,
        string secretId,
        string changeType,
        JsonElement? resourceValues,
        CancellationToken cancellationToken
    )
    {
        var auditEvent = new ServerSecretChangedAuditEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = TimeProvider.GetUtcNow(),
            CorrelationId = GetCorrelationId(httpContext),
            ActorId = GetActorId(httpContext),
            Source = GetSource(httpContext),
            Outcome = AuditOutcome.Success,
            ServerId = serverId,
            ResourceId = secretId,
            ChangeType = changeType,
            ResourceValues = resourceValues,
        };

        return EventPublisher.PublishAsync(auditEvent, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask RecordResourceServerChangedAsync(
        HttpContext httpContext,
        string tenantId,
        string resourceServerId,
        string changeType,
        JsonElement? resourceValues,
        CancellationToken cancellationToken
    )
    {
        var auditEvent = new ResourceServerChangedAuditEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = TimeProvider.GetUtcNow(),
            CorrelationId = GetCorrelationId(httpContext),
            ActorId = GetActorId(httpContext),
            Source = GetSource(httpContext),
            TenantId = tenantId,
            Outcome = AuditOutcome.Success,
            ResourceId = resourceServerId,
            ChangeType = changeType,
            ResourceValues = resourceValues,
        };

        return EventPublisher.PublishAsync(auditEvent, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask RecordResourceServerScopeChangedAsync(
        HttpContext httpContext,
        string tenantId,
        string resourceServerId,
        string scopeValue,
        string changeType,
        JsonElement? resourceValues,
        CancellationToken cancellationToken
    )
    {
        var auditEvent = new ResourceServerScopeChangedAuditEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = TimeProvider.GetUtcNow(),
            CorrelationId = GetCorrelationId(httpContext),
            ActorId = GetActorId(httpContext),
            Source = GetSource(httpContext),
            TenantId = tenantId,
            Outcome = AuditOutcome.Success,
            ResourceServerId = resourceServerId,
            ResourceId = scopeValue,
            ChangeType = changeType,
            ResourceValues = resourceValues,
        };

        return EventPublisher.PublishAsync(auditEvent, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask RecordResourceServerSecretChangedAsync(
        HttpContext httpContext,
        string tenantId,
        string resourceServerId,
        string secretId,
        string changeType,
        JsonElement? resourceValues,
        CancellationToken cancellationToken
    )
    {
        var auditEvent = new ResourceServerSecretChangedAuditEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = TimeProvider.GetUtcNow(),
            CorrelationId = GetCorrelationId(httpContext),
            ActorId = GetActorId(httpContext),
            Source = GetSource(httpContext),
            TenantId = tenantId,
            Outcome = AuditOutcome.Success,
            ResourceServerId = resourceServerId,
            ResourceId = secretId,
            ChangeType = changeType,
            ResourceValues = resourceValues,
        };

        return EventPublisher.PublishAsync(auditEvent, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask RecordResourceChangeAsync(
        HttpContext httpContext,
        ResourceChangeAuditEvent auditEvent,
        CancellationToken cancellationToken
    )
    {
        // Stamp the common envelope (the caller owns the resource-specific fields and outcome); `with` preserves the
        // runtime event type so dispatch keys off the concrete event.
        var stamped = auditEvent with
        {
            EventId = Guid.NewGuid(),
            Timestamp = TimeProvider.GetUtcNow(),
            CorrelationId = GetCorrelationId(httpContext),
            ActorId = GetActorId(httpContext),
            Source = GetSource(httpContext),
        };

        return EventPublisher.PublishAsync(stamped, cancellationToken);
    }

    private static string? GetCorrelationId(HttpContext httpContext) =>
        Activity.Current?.Id ?? httpContext.TraceIdentifier;

    private static string? GetSource(HttpContext httpContext)
    {
        var endpoint = httpContext.GetEndpoint();
        return endpoint?.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName
            ?? endpoint?.DisplayName;
    }

    // The actor is a self-issued/control-plane admin, so its subject is read with the server-default claim types
    // (ADR-0053) through the app-level environment — not the request tenant's subject_claim_types setting.
    private string? GetActorId(HttpContext httpContext) =>
        EnvironmentProvider
            .Get()
            .GetSubjectId(httpContext.User, OpenIdConstants.DefaultSubjectClaimTypes);
}
