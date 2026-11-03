// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Events;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Settings;

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

    private static AuditRequest CreateRequest(OpenIdContext openIdContext) =>
        AuditRequestFactory.Create(openIdContext.Http);

    private static string GetTenantEnvironmentType(OpenIdContext openIdContext) =>
        openIdContext.Tenant.SettingsProvider.Collection.GetValue(
            OpenIdSettingKeys.EnvironmentType
        );

    /// <inheritdoc />
    public ValueTask RecordTokenIssuedAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        string? subjectId,
        SecurityToken securityToken,
        CancellationToken cancellationToken
    )
    {
        var now = TimeProvider.GetUtcNow();
        var auditEvent = new TokenIssuedAuditEvent
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = AuditEventSources.OpenId,
            TenantId = openIdContext.Tenant.TenantId,
            TenantEnvironmentType = GetTenantEnvironmentType(openIdContext),
            SubjectId = subjectId,
            ClientId = openIdClient.ClientId,
            Actor = AuditActor.Client(openIdClient.ClientId),
            Request = CreateRequest(openIdContext),
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
        var now = TimeProvider.GetUtcNow();
        var auditEvent = new TokenRevokedAuditEvent
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = AuditEventSources.OpenId,
            TenantId = openIdContext.Tenant.TenantId,
            TenantEnvironmentType = GetTenantEnvironmentType(openIdContext),
            SubjectId = subjectId,
            ClientId = openIdClient.ClientId,
            Actor = AuditActor.Client(openIdClient.ClientId),
            Request = CreateRequest(openIdContext),
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
        var now = TimeProvider.GetUtcNow();
        var auditEvent = new AuthorizationGrantedAuditEvent
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = AuditEventSources.OpenId,
            TenantId = openIdContext.Tenant.TenantId,
            TenantEnvironmentType = GetTenantEnvironmentType(openIdContext),
            SubjectId = subjectId,
            ClientId = openIdClient.ClientId,
            Actor = AuditActor.EndUser(subjectId),
            Request = CreateRequest(openIdContext),
            Outcome = AuditOutcome.Success,
        };

        return EventPublisher.PublishAsync(auditEvent, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask RecordAuthorizationDeniedAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        string? subjectId,
        string? errorCode,
        CancellationToken cancellationToken
    )
    {
        var now = TimeProvider.GetUtcNow();
        var auditEvent = new AuthorizationDeniedAuditEvent
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = AuditEventSources.OpenId,
            TenantId = openIdContext.Tenant.TenantId,
            TenantEnvironmentType = GetTenantEnvironmentType(openIdContext),
            SubjectId = subjectId,
            ClientId = openIdClient.ClientId,
            Actor = AuditActor.EndUser(subjectId),
            Request = CreateRequest(openIdContext),
            Outcome = AuditOutcome.Denied,
            Error = new AuditError(errorCode ?? OpenIdProtocol.ErrorCodes.AccessDenied),
        };

        return EventPublisher.PublishAsync(auditEvent, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask RecordClientAuthenticationFailedAsync(
        OpenIdContext openIdContext,
        string clientId,
        string authenticationMethod,
        string? errorCode,
        CancellationToken cancellationToken
    )
    {
        var now = TimeProvider.GetUtcNow();
        var auditEvent = new ClientAuthenticationFailedAuditEvent
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = AuditEventSources.OpenId,
            TenantId = openIdContext.Tenant.TenantId,
            TenantEnvironmentType = GetTenantEnvironmentType(openIdContext),
            ClientId = clientId,
            Actor = AuditActor.Client(clientId),
            Request = CreateRequest(openIdContext),
            AuthenticationMethod = authenticationMethod,
            Outcome = AuditOutcome.Failure,
            Error = new AuditError(errorCode ?? OpenIdProtocol.ErrorCodes.InvalidClient),
        };

        return EventPublisher.PublishAsync(auditEvent, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask RecordSubjectAuthenticatedAsync(
        OpenIdContext openIdContext,
        string subjectId,
        CancellationToken cancellationToken
    )
    {
        var now = TimeProvider.GetUtcNow();
        var auditEvent = new SubjectAuthenticationAuditEvent
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = AuditEventSources.OpenId,
            TenantId = openIdContext.Tenant.TenantId,
            TenantEnvironmentType = GetTenantEnvironmentType(openIdContext),
            SubjectId = subjectId,
            Actor = AuditActor.EndUser(subjectId),
            Request = CreateRequest(openIdContext),
            Outcome = AuditOutcome.Success,
        };

        return EventPublisher.PublishAsync(auditEvent, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask RecordSubjectAuthenticationFailedAsync(
        OpenIdContext openIdContext,
        string errorCode,
        string? errorMessage,
        string? attemptedUsername,
        CancellationToken cancellationToken
    )
    {
        var now = TimeProvider.GetUtcNow();
        var auditEvent = new SubjectAuthenticationAuditEvent
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = AuditEventSources.OpenId,
            TenantId = openIdContext.Tenant.TenantId,
            TenantEnvironmentType = GetTenantEnvironmentType(openIdContext),
            AttemptedUsername = attemptedUsername,
            Actor = AuditActor.Anonymous,
            Request = CreateRequest(openIdContext),
            Outcome = AuditOutcome.Failure,
            Error = new AuditError(errorCode, errorMessage),
        };

        return EventPublisher.PublishAsync(auditEvent, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask RecordRefreshTokenReplayAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        string? subjectId,
        CancellationToken cancellationToken
    )
    {
        var now = TimeProvider.GetUtcNow();
        var auditEvent = new RefreshTokenReplayAuditEvent
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = AuditEventSources.OpenId,
            TenantId = openIdContext.Tenant.TenantId,
            TenantEnvironmentType = GetTenantEnvironmentType(openIdContext),
            SubjectId = subjectId,
            ClientId = openIdClient.ClientId,
            Actor = AuditActor.Client(openIdClient.ClientId),
            Request = CreateRequest(openIdContext),
            Outcome = AuditOutcome.Denied,
        };

        return EventPublisher.PublishAsync(auditEvent, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask RecordRefreshTokenRetryAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        string? subjectId,
        CancellationToken cancellationToken
    )
    {
        var now = TimeProvider.GetUtcNow();
        var auditEvent = new RefreshTokenRetryAuditEvent
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = AuditEventSources.OpenId,
            TenantId = openIdContext.Tenant.TenantId,
            TenantEnvironmentType = GetTenantEnvironmentType(openIdContext),
            SubjectId = subjectId,
            ClientId = openIdClient.ClientId,
            Actor = AuditActor.Client(openIdClient.ClientId),
            Request = CreateRequest(openIdContext),
            Outcome = AuditOutcome.Success,
        };

        return EventPublisher.PublishAsync(auditEvent, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask RecordOpenIdErrorAsync(
        OpenIdContext openIdContext,
        IOpenIdError error,
        CancellationToken cancellationToken
    )
    {
        var outcome = string.Equals(
            error.Code,
            OpenIdProtocol.ErrorCodes.AccessDenied,
            StringComparison.Ordinal
        )
            ? AuditOutcome.Denied
            : AuditOutcome.Failure;

        var now = TimeProvider.GetUtcNow();
        var auditEvent = new OpenIdErrorEvent
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = AuditEventSources.OpenId,
            TenantId = openIdContext.Tenant.TenantId,
            TenantEnvironmentType = GetTenantEnvironmentType(openIdContext),
            Actor = AuditActor.Anonymous,
            Request = CreateRequest(openIdContext),
            Outcome = outcome,
            Error = new AuditError(error.Code, error.Description, error.StatusCode),
        };

        return EventPublisher.PublishAsync(auditEvent, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask RecordSessionCreatedAsync(
        OpenIdContext openIdContext,
        string subjectId,
        string sessionId,
        CancellationToken cancellationToken
    )
    {
        var now = TimeProvider.GetUtcNow();
        var auditEvent = new SessionCreatedAuditEvent
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = AuditEventSources.OpenId,
            TenantId = openIdContext.Tenant.TenantId,
            TenantEnvironmentType = GetTenantEnvironmentType(openIdContext),
            SubjectId = subjectId,
            Actor = AuditActor.EndUser(subjectId),
            Request = CreateRequest(openIdContext),
            Outcome = AuditOutcome.Success,
            SessionId = sessionId,
        };

        return EventPublisher.PublishAsync(auditEvent, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask RecordSessionTerminatedAsync(
        OpenIdContext openIdContext,
        string reason,
        string? subjectId,
        string? sessionId,
        CancellationToken cancellationToken
    )
    {
        var now = TimeProvider.GetUtcNow();
        var auditEvent = new SessionTerminatedAuditEvent
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = AuditEventSources.OpenId,
            TenantId = openIdContext.Tenant.TenantId,
            TenantEnvironmentType = GetTenantEnvironmentType(openIdContext),
            SubjectId = subjectId,
            Actor = AuditActor.System,
            Request = CreateRequest(openIdContext),
            Outcome = AuditOutcome.Success,
            SessionId = sessionId,
            TerminationReason = reason,
        };

        return EventPublisher.PublishAsync(auditEvent, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask RecordUserInfoAccessedAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        string subjectId,
        IEnumerable<string> claimNames,
        CancellationToken cancellationToken
    )
    {
        var now = TimeProvider.GetUtcNow();
        var auditEvent = new UserInfoAccessedAuditEvent
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = AuditEventSources.OpenId,
            TenantId = openIdContext.Tenant.TenantId,
            TenantEnvironmentType = GetTenantEnvironmentType(openIdContext),
            SubjectId = subjectId,
            ClientId = openIdClient.ClientId,
            Actor = AuditActor.Client(openIdClient.ClientId),
            Request = CreateRequest(openIdContext),
            Outcome = AuditOutcome.Success,
            ClaimNames = SortDistinct(claimNames),
        };

        return EventPublisher.PublishAsync(auditEvent, cancellationToken);
    }

    // The names of a response's claims in ordinal order, each once: one array, sorted in place and compacted, so an
    // audited response allocates no enumerator or delegate beyond the record it publishes.
    private static string[] SortDistinct(IEnumerable<string> claimNames)
    {
        var names = claimNames.ToArray();
        if (names.Length < 2)
            return names;

        Array.Sort(names, StringComparer.Ordinal);

        var count = 1;
        for (var i = 1; i < names.Length; i++)
        {
            if (!string.Equals(names[i], names[count - 1], StringComparison.Ordinal))
                names[count++] = names[i];
        }

        if (count == names.Length)
            return names;

        Array.Resize(ref names, count);
        return names;
    }

    /// <inheritdoc />
    public ValueTask RecordUserInfoAccessDeniedAsync(
        OpenIdContext openIdContext,
        string subjectId,
        string? clientId,
        string errorCode,
        CancellationToken cancellationToken
    )
    {
        var now = TimeProvider.GetUtcNow();
        var auditEvent = new UserInfoAccessedAuditEvent
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = AuditEventSources.OpenId,
            TenantId = openIdContext.Tenant.TenantId,
            TenantEnvironmentType = GetTenantEnvironmentType(openIdContext),
            SubjectId = subjectId,
            ClientId = clientId,
            Actor = clientId is null ? AuditActor.Anonymous : AuditActor.Client(clientId),
            Request = CreateRequest(openIdContext),
            Outcome = AuditOutcome.Denied,
            Error = new AuditError(errorCode),
            ClaimNames = [],
        };

        return EventPublisher.PublishAsync(auditEvent, cancellationToken);
    }
}
