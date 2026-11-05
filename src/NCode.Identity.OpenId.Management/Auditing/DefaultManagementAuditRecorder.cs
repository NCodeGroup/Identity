// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NCode.Identity.Events;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Management.Logging;
using NCode.Identity.Persistence;
using NCode.Identity.Secrets.Persistence.Auditing;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.OpenId.Management.Auditing;

/// <summary>
/// Provides the default implementation of <see cref="IManagementAuditRecorder"/> that derives the audit
/// envelope — including the acting administrator and the effective environment type of the tenant the event names —
/// from the current request and publishes through the event seam.
/// </summary>
internal class DefaultManagementAuditRecorder(
    IEventPublisher eventPublisher,
    IOpenIdEnvironmentProvider environmentProvider,
    IManagementTenantEnvironmentResolver tenantEnvironmentResolver,
    TimeProvider timeProvider,
    ILogger<DefaultManagementAuditRecorder> logger
) : IManagementAuditRecorder
{
    private IEventPublisher EventPublisher { get; } = eventPublisher;
    private IOpenIdEnvironmentProvider EnvironmentProvider { get; } = environmentProvider;
    private IManagementTenantEnvironmentResolver TenantEnvironmentResolver { get; } =
        tenantEnvironmentResolver;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private ILogger<DefaultManagementAuditRecorder> Logger { get; } = logger;

    /// <inheritdoc />
    public async ValueTask RecordGrantRevokedAsync(
        HttpContext httpContext,
        string tenantId,
        string? subjectId,
        string? clientId,
        string? grantType,
        CancellationToken cancellationToken
    )
    {
        var now = TimeProvider.GetUtcNow();
        var auditEvent = new GrantRevokedAuditEvent
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = AuditEventSources.Management,
            TenantId = tenantId,
            TenantEnvironmentType = await TenantEnvironmentResolver.GetEnvironmentTypeAsync(
                httpContext,
                tenantId,
                cancellationToken
            ),
            SubjectId = subjectId,
            ClientId = clientId,
            Actor = GetActor(httpContext),
            Request = CreateRequest(httpContext),
            Outcome = AuditOutcome.Success,
            GrantType = grantType,
        };

        await PublishAsync(auditEvent);
    }

    /// <inheritdoc />
    public async ValueTask RecordGrantsRevokedAsync(
        HttpContext httpContext,
        string tenantId,
        string? subjectId,
        string? clientId,
        string? sessionId,
        long revokedCount,
        CancellationToken cancellationToken
    )
    {
        var now = TimeProvider.GetUtcNow();
        var auditEvent = new GrantsRevokedBulkAuditEvent
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = AuditEventSources.Management,
            TenantId = tenantId,
            TenantEnvironmentType = await TenantEnvironmentResolver.GetEnvironmentTypeAsync(
                httpContext,
                tenantId,
                cancellationToken
            ),
            SubjectId = subjectId,
            ClientId = clientId,
            Actor = GetActor(httpContext),
            Request = CreateRequest(httpContext),
            Outcome = AuditOutcome.Success,
            SessionId = sessionId,
            RevokedCount = revokedCount,
        };

        await PublishAsync(auditEvent);
    }

    /// <inheritdoc />
    public async ValueTask RecordSessionsRevokedAsync(
        HttpContext httpContext,
        string tenantId,
        string? subjectId,
        string? sessionId,
        long revokedCount,
        CancellationToken cancellationToken
    )
    {
        var now = TimeProvider.GetUtcNow();
        var auditEvent = new SessionsRevokedAuditEvent
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = AuditEventSources.Management,
            TenantId = tenantId,
            TenantEnvironmentType = await TenantEnvironmentResolver.GetEnvironmentTypeAsync(
                httpContext,
                tenantId,
                cancellationToken
            ),
            SubjectId = subjectId,
            Actor = GetActor(httpContext),
            Request = CreateRequest(httpContext),
            Outcome = AuditOutcome.Success,
            SessionId = sessionId,
            RevokedCount = revokedCount,
        };

        await PublishAsync(auditEvent);
    }

    /// <inheritdoc />
    public async ValueTask RecordClientChangedAsync(
        HttpContext httpContext,
        string tenantId,
        string clientId,
        string changeType,
        JsonElement? resourceValues,
        CancellationToken cancellationToken
    )
    {
        var now = TimeProvider.GetUtcNow();
        var auditEvent = new ClientChangedAuditEvent
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = AuditEventSources.Management,
            TenantId = tenantId,
            TenantEnvironmentType = await TenantEnvironmentResolver.GetEnvironmentTypeAsync(
                httpContext,
                tenantId,
                cancellationToken
            ),
            ClientId = clientId,
            Actor = GetActor(httpContext),
            Request = CreateRequest(httpContext),
            Outcome = AuditOutcome.Success,
            ResourceId = clientId,
            ChangeType = changeType,
            ResourceValues = resourceValues,
        };

        await PublishAsync(auditEvent);
    }

    /// <inheritdoc />
    public async ValueTask RecordClientSecretChangedAsync(
        HttpContext httpContext,
        string tenantId,
        string clientId,
        string secretId,
        string changeType,
        JsonElement? resourceValues,
        CancellationToken cancellationToken
    )
    {
        var now = TimeProvider.GetUtcNow();
        var auditEvent = new ClientSecretChangedAuditEvent
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = AuditEventSources.Management,
            TenantId = tenantId,
            TenantEnvironmentType = await TenantEnvironmentResolver.GetEnvironmentTypeAsync(
                httpContext,
                tenantId,
                cancellationToken
            ),
            ClientId = clientId,
            Actor = GetActor(httpContext),
            Request = CreateRequest(httpContext),
            Outcome = AuditOutcome.Success,
            ResourceId = secretId,
            ChangeType = changeType,
            ResourceValues = resourceValues,
        };

        await PublishAsync(auditEvent);
    }

    /// <inheritdoc />
    public async ValueTask RecordTenantChangedAsync(
        HttpContext httpContext,
        string tenantId,
        string changeType,
        JsonElement? resourceValues,
        CancellationToken cancellationToken
    )
    {
        var now = TimeProvider.GetUtcNow();
        var auditEvent = new TenantChangedAuditEvent
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = AuditEventSources.Management,
            TenantId = tenantId,
            TenantEnvironmentType = await TenantEnvironmentResolver.GetEnvironmentTypeAsync(
                httpContext,
                tenantId,
                cancellationToken
            ),
            Actor = GetActor(httpContext),
            Request = CreateRequest(httpContext),
            Outcome = AuditOutcome.Success,
            ResourceId = tenantId,
            ChangeType = changeType,
            ResourceValues = resourceValues,
        };

        await PublishAsync(auditEvent);
    }

    /// <inheritdoc />
    public async ValueTask RecordTenantSecretChangedAsync(
        HttpContext httpContext,
        string tenantId,
        string secretId,
        string changeType,
        JsonElement? resourceValues,
        CancellationToken cancellationToken
    )
    {
        var now = TimeProvider.GetUtcNow();
        var auditEvent = new TenantSecretChangedAuditEvent
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = AuditEventSources.Management,
            TenantId = tenantId,
            TenantEnvironmentType = await TenantEnvironmentResolver.GetEnvironmentTypeAsync(
                httpContext,
                tenantId,
                cancellationToken
            ),
            Actor = GetActor(httpContext),
            Request = CreateRequest(httpContext),
            Outcome = AuditOutcome.Success,
            ResourceId = secretId,
            ChangeType = changeType,
            ResourceValues = resourceValues,
        };

        await PublishAsync(auditEvent);
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
        var now = TimeProvider.GetUtcNow();
        var auditEvent = new ServerChangedAuditEvent
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = AuditEventSources.Management,
            Actor = GetActor(httpContext),
            Request = CreateRequest(httpContext),
            Outcome = AuditOutcome.Success,
            ResourceId = serverId,
            ChangeType = changeType,
            ResourceValues = resourceValues,
        };

        return PublishAsync(auditEvent);
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
        var now = TimeProvider.GetUtcNow();
        var auditEvent = new ServerSecretChangedAuditEvent
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = AuditEventSources.Management,
            Actor = GetActor(httpContext),
            Request = CreateRequest(httpContext),
            Outcome = AuditOutcome.Success,
            ServerId = serverId,
            ResourceId = secretId,
            ChangeType = changeType,
            ResourceValues = resourceValues,
        };

        return PublishAsync(auditEvent);
    }

    /// <inheritdoc />
    public async ValueTask RecordResourceServerChangedAsync(
        HttpContext httpContext,
        string tenantId,
        string resourceServerId,
        string changeType,
        JsonElement? resourceValues,
        CancellationToken cancellationToken
    )
    {
        var now = TimeProvider.GetUtcNow();
        var auditEvent = new ResourceServerChangedAuditEvent
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = AuditEventSources.Management,
            TenantId = tenantId,
            TenantEnvironmentType = await TenantEnvironmentResolver.GetEnvironmentTypeAsync(
                httpContext,
                tenantId,
                cancellationToken
            ),
            Actor = GetActor(httpContext),
            Request = CreateRequest(httpContext),
            Outcome = AuditOutcome.Success,
            ResourceId = resourceServerId,
            ChangeType = changeType,
            ResourceValues = resourceValues,
        };

        await PublishAsync(auditEvent);
    }

    /// <inheritdoc />
    public async ValueTask RecordResourceServerScopeChangedAsync(
        HttpContext httpContext,
        string tenantId,
        string resourceServerId,
        string scopeValue,
        string changeType,
        JsonElement? resourceValues,
        CancellationToken cancellationToken
    )
    {
        var now = TimeProvider.GetUtcNow();
        var auditEvent = new ResourceServerScopeChangedAuditEvent
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = AuditEventSources.Management,
            TenantId = tenantId,
            TenantEnvironmentType = await TenantEnvironmentResolver.GetEnvironmentTypeAsync(
                httpContext,
                tenantId,
                cancellationToken
            ),
            Actor = GetActor(httpContext),
            Request = CreateRequest(httpContext),
            Outcome = AuditOutcome.Success,
            ResourceServerId = resourceServerId,
            ResourceId = scopeValue,
            ChangeType = changeType,
            ResourceValues = resourceValues,
        };

        await PublishAsync(auditEvent);
    }

    /// <inheritdoc />
    public async ValueTask RecordResourceServerSecretChangedAsync(
        HttpContext httpContext,
        string tenantId,
        string resourceServerId,
        string secretId,
        string changeType,
        JsonElement? resourceValues,
        CancellationToken cancellationToken
    )
    {
        var now = TimeProvider.GetUtcNow();
        var auditEvent = new ResourceServerSecretChangedAuditEvent
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = AuditEventSources.Management,
            TenantId = tenantId,
            TenantEnvironmentType = await TenantEnvironmentResolver.GetEnvironmentTypeAsync(
                httpContext,
                tenantId,
                cancellationToken
            ),
            Actor = GetActor(httpContext),
            Request = CreateRequest(httpContext),
            Outcome = AuditOutcome.Success,
            ResourceServerId = resourceServerId,
            ResourceId = secretId,
            ChangeType = changeType,
            ResourceValues = resourceValues,
        };

        await PublishAsync(auditEvent);
    }

    /// <inheritdoc />
    public async ValueTask RecordResourceChangeAsync(
        HttpContext httpContext,
        ResourceChangeAuditEvent auditEvent,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        var tenantEnvironmentType = auditEvent.TenantEnvironmentType;
        if (tenantEnvironmentType is null && auditEvent.TenantId is { } tenantId)
        {
            tenantEnvironmentType = await TenantEnvironmentResolver.GetEnvironmentTypeAsync(
                httpContext,
                tenantId,
                cancellationToken
            );
        }

        // Stamp the common envelope (the caller owns the resource-specific fields and outcome); `with` preserves the
        // runtime event type so dispatch keys off the concrete event.
        var now = TimeProvider.GetUtcNow();
        var stamped = auditEvent with
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = AuditEventSources.Management,
            TenantEnvironmentType = tenantEnvironmentType,
            Actor = GetActor(httpContext),
            Request = CreateRequest(httpContext),
        };

        await PublishAsync(stamped);
    }

    public async ValueTask RecordMasterKeyRotatedAsync(
        HttpContext httpContext,
        MasterKeyRotation rotation,
        string reason,
        CancellationToken cancellationToken
    )
    {
        var now = TimeProvider.GetUtcNow();
        var owner = rotation.Owner;
        var tenantId = owner.TenantId;

        var auditEvent = new MasterKeyAuditEvent
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = AuditEventSources.Management,
            TenantId = tenantId,
            TenantEnvironmentType = tenantId is null
                ? null
                : await TenantEnvironmentResolver.GetEnvironmentTypeAsync(
                    httpContext,
                    tenantId,
                    cancellationToken
                ),
            Actor = GetActor(httpContext),
            Request = CreateRequest(httpContext),
            Outcome = AuditOutcome.Success,
            Change = MasterKeyChanges.Rotated,
            Reason = reason,
            OwnerType = owner.OwnerType,
            OwnerId = owner.OwnerId,
            KeyId = rotation.ActiveKey.KeyId,
            Version = rotation.ActiveKey.Version,
            RootKeyProvider = rotation.ActiveKey.RootKeyProvider,
            RootKeyReference = rotation.ActiveKey.RootKeyReference,
            RetiredKeyId = rotation.RetiredKey?.KeyId,
            RetiredVersion = rotation.RetiredKey?.Version,
        };

        await PublishAsync(auditEvent);
    }

    /// <inheritdoc />
    public async ValueTask RecordRootKeyChangedAsync(
        HttpContext httpContext,
        OwnerRootKeyInfo rootKey,
        string change,
        string reason,
        CancellationToken cancellationToken
    )
    {
        var now = TimeProvider.GetUtcNow();
        var tenantId = rootKey.Owner.TenantId;

        var auditEvent = RootKeyAuditEvents.Create(
            rootKey,
            change,
            reason,
            AuditEventSources.Management,
            GetActor(httpContext),
            now
        );
        auditEvent = auditEvent with
        {
            TenantEnvironmentType = tenantId is null
                ? null
                : await TenantEnvironmentResolver.GetEnvironmentTypeAsync(
                    httpContext,
                    tenantId,
                    cancellationToken
                ),
            Request = CreateRequest(httpContext),
        };

        await PublishAsync(auditEvent);
    }

    // The event is published once the change is committed, on a bounded token of its own, so the request's cancellation
    // never drops the audit record. A failure, its timeout included, is logged and never turns a committed change into
    // an error response.
    private async ValueTask PublishAsync<TEvent>(TEvent auditEvent)
        where TEvent : IEvent
    {
        try
        {
            await PostCommitWork.RunAsync(
                TimeProvider,
                (state, cancellationToken) =>
                    state.EventPublisher.PublishAsync(state.AuditEvent, cancellationToken),
                (EventPublisher, AuditEvent: auditEvent)
            );
        }
        catch (Exception exception)
        {
            Logger.AuditPublishFailed(exception, auditEvent.GetType().Name);
        }
    }

    private static AuditRequest CreateRequest(HttpContext httpContext) =>
        AuditRequestFactory.Create(httpContext);

    private AuditActor GetActor(HttpContext httpContext) =>
        AuditActor.Administrator(GetActorId(httpContext));

    // The actor is a self-issued/control-plane admin, so its subject is read with the server-default claim types
    // (ADR-1053) through the app-level environment — not the request tenant's subject_claim_types setting.
    private string? GetActorId(HttpContext httpContext) =>
        EnvironmentProvider
            .Get()
            .GetSubjectId(httpContext.User, OpenIdConstants.DefaultSubjectClaimTypes);
}
