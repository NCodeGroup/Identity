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
using NCode.Identity.Events;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Auditing;

/// <summary>
/// Provides the default implementation of <see cref="IAuditEventRecorder"/> that derives the audit
/// envelope from the ambient request context and publishes through the event seam.
/// </summary>
internal class DefaultAuditEventRecorder(IEventPublisher eventPublisher, TimeProvider timeProvider)
    : IAuditEventRecorder
{
    private IEventPublisher EventPublisher { get; } = eventPublisher;
    private TimeProvider TimeProvider { get; } = timeProvider;

    /// <inheritdoc />
    public ValueTask RecordTokenIssuedAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        string? subjectId,
        SecurityToken securityToken,
        CancellationToken cancellationToken
    )
    {
        var auditEvent = new TokenIssuedAuditEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = TimeProvider.GetUtcNow(),
            CorrelationId = Activity.Current?.Id,
            TenantId = openIdContext.Tenant.TenantId,
            SubjectId = subjectId,
            ClientId = openIdClient.ClientId,
            Outcome = AuditOutcome.Success,
            TokenType = securityToken.TokenType,
        };

        return EventPublisher.PublishAsync(auditEvent, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask RecordTokenRevokedAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        string? subjectId,
        CancellationToken cancellationToken
    )
    {
        var auditEvent = new TokenRevokedAuditEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = TimeProvider.GetUtcNow(),
            CorrelationId = Activity.Current?.Id,
            TenantId = openIdContext.Tenant.TenantId,
            SubjectId = subjectId,
            ClientId = openIdClient.ClientId,
            Outcome = AuditOutcome.Success,
        };

        return EventPublisher.PublishAsync(auditEvent, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask RecordAuthorizationGrantedAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        string? subjectId,
        CancellationToken cancellationToken
    )
    {
        var auditEvent = new AuthorizationGrantedAuditEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = TimeProvider.GetUtcNow(),
            CorrelationId = Activity.Current?.Id,
            TenantId = openIdContext.Tenant.TenantId,
            SubjectId = subjectId,
            ClientId = openIdClient.ClientId,
            Outcome = AuditOutcome.Success,
        };

        return EventPublisher.PublishAsync(auditEvent, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask RecordAuthorizationDeniedAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        string? subjectId,
        string? reason,
        CancellationToken cancellationToken
    )
    {
        var auditEvent = new AuthorizationDeniedAuditEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = TimeProvider.GetUtcNow(),
            CorrelationId = Activity.Current?.Id,
            TenantId = openIdContext.Tenant.TenantId,
            SubjectId = subjectId,
            ClientId = openIdClient.ClientId,
            Outcome = AuditOutcome.Denied,
            Reason = reason,
        };

        return EventPublisher.PublishAsync(auditEvent, cancellationToken);
    }
}
