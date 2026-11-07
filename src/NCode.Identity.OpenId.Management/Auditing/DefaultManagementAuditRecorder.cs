// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NCode.Identity.Events;
using NCode.Identity.Events.Audit;
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Management.Logging;
using NCode.Identity.Persistence;
using NCode.Identity.Secrets.Persistence.Auditing;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.OpenId.Management.Auditing;

/// <summary>
/// Provides the default implementation of <see cref="IManagementAuditRecorder"/> that derives the audit
/// envelope — including the acting administrator and the effective environment type of the tenant the event names —
/// from the current request and publishes through the event seam. The whole record, resolving the tenant's
/// environment type included, runs on its own bounded token and never takes the request's cancellation.
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
    public ValueTask RecordGrantRevokedAsync(
        HttpContext httpContext,
        string tenantId,
        string? subjectId,
        string? clientId,
        string? grantType
    )
    {
        var now = TimeProvider.GetUtcNow();
        var auditEvent = new GrantRevokedAuditEvent
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = AuditEventSources.Management,
            TenantId = tenantId,
            SubjectId = subjectId,
            ClientId = clientId,
            Actor = GetActor(httpContext),
            Request = CreateRequest(httpContext),
            Outcome = AuditOutcome.Success,
            GrantType = grantType,
        };

        return RecordAsync(httpContext, tenantId, auditEvent);
    }

    /// <inheritdoc />
    public ValueTask RecordGrantsRevokedAsync(
        HttpContext httpContext,
        string tenantId,
        string? subjectId,
        string? clientId,
        string? sessionId,
        long revokedCount
    )
    {
        var now = TimeProvider.GetUtcNow();
        var auditEvent = new GrantsRevokedBulkAuditEvent
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = AuditEventSources.Management,
            TenantId = tenantId,
            SubjectId = subjectId,
            ClientId = clientId,
            Actor = GetActor(httpContext),
            Request = CreateRequest(httpContext),
            Outcome = AuditOutcome.Success,
            SessionId = sessionId,
            RevokedCount = revokedCount,
        };

        return RecordAsync(httpContext, tenantId, auditEvent);
    }

    /// <inheritdoc />
    public ValueTask RecordSessionsRevokedAsync(
        HttpContext httpContext,
        string tenantId,
        string? subjectId,
        string? sessionId,
        long revokedCount
    )
    {
        var now = TimeProvider.GetUtcNow();
        var auditEvent = new SessionsRevokedAuditEvent
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = AuditEventSources.Management,
            TenantId = tenantId,
            SubjectId = subjectId,
            Actor = GetActor(httpContext),
            Request = CreateRequest(httpContext),
            Outcome = AuditOutcome.Success,
            SessionId = sessionId,
            RevokedCount = revokedCount,
        };

        return RecordAsync(httpContext, tenantId, auditEvent);
    }

    /// <inheritdoc />
    public ValueTask RecordClientChangedAsync(
        HttpContext httpContext,
        string tenantId,
        string clientId,
        string changeType,
        JsonElement? resourceValues
    )
    {
        var now = TimeProvider.GetUtcNow();
        var auditEvent = new ClientChangedAuditEvent
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = AuditEventSources.Management,
            TenantId = tenantId,
            ClientId = clientId,
            Actor = GetActor(httpContext),
            Request = CreateRequest(httpContext),
            Outcome = AuditOutcome.Success,
            ResourceId = clientId,
            ChangeType = changeType,
            ResourceValues = resourceValues,
        };

        return RecordAsync(httpContext, tenantId, auditEvent);
    }

    /// <inheritdoc />
    public ValueTask RecordClientSecretChangedAsync(
        HttpContext httpContext,
        string tenantId,
        string clientId,
        string secretId,
        string changeType,
        JsonElement? resourceValues
    )
    {
        var now = TimeProvider.GetUtcNow();
        var auditEvent = new ClientSecretChangedAuditEvent
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = AuditEventSources.Management,
            TenantId = tenantId,
            ClientId = clientId,
            Actor = GetActor(httpContext),
            Request = CreateRequest(httpContext),
            Outcome = AuditOutcome.Success,
            ResourceId = secretId,
            ChangeType = changeType,
            ResourceValues = resourceValues,
        };

        return RecordAsync(httpContext, tenantId, auditEvent);
    }

    /// <inheritdoc />
    public ValueTask RecordTeamChangedAsync(
        HttpContext httpContext,
        string teamId,
        string changeType,
        JsonElement? resourceValues
    )
    {
        var now = TimeProvider.GetUtcNow();
        var auditEvent = new TeamChangedAuditEvent
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = AuditEventSources.Management,
            TenantId = httpContext.GetOpenIdContext().Tenant.TenantId,
            Actor = GetActor(httpContext),
            Request = CreateRequest(httpContext),
            Outcome = AuditOutcome.Success,
            ResourceId = teamId,
            ChangeType = changeType,
            ResourceValues = resourceValues,
        };

        return RecordAsync(httpContext, null, auditEvent);
    }

    /// <inheritdoc />
    public ValueTask RecordTenantChangedAsync(
        HttpContext httpContext,
        string teamId,
        string tenantId,
        string changeType,
        JsonElement? resourceValues
    )
    {
        var now = TimeProvider.GetUtcNow();
        var auditEvent = new TenantChangedAuditEvent
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = AuditEventSources.Management,
            TenantId = tenantId,
            Actor = GetActor(httpContext),
            Request = CreateRequest(httpContext),
            Outcome = AuditOutcome.Success,
            TeamId = teamId,
            ResourceId = tenantId,
            ChangeType = changeType,
            ResourceValues = resourceValues,
        };

        return RecordAsync(httpContext, tenantId, auditEvent);
    }

    /// <inheritdoc />
    public ValueTask RecordTenantSecretChangedAsync(
        HttpContext httpContext,
        string tenantId,
        string secretId,
        string changeType,
        JsonElement? resourceValues
    )
    {
        var now = TimeProvider.GetUtcNow();
        var auditEvent = new TenantSecretChangedAuditEvent
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = AuditEventSources.Management,
            TenantId = tenantId,
            Actor = GetActor(httpContext),
            Request = CreateRequest(httpContext),
            Outcome = AuditOutcome.Success,
            ResourceId = secretId,
            ChangeType = changeType,
            ResourceValues = resourceValues,
        };

        return RecordAsync(httpContext, tenantId, auditEvent);
    }

    /// <inheritdoc />
    public ValueTask RecordPlatformChangedAsync(
        HttpContext httpContext,
        string changeType,
        JsonElement? resourceValues
    )
    {
        var now = TimeProvider.GetUtcNow();
        var auditEvent = new PlatformChangedAuditEvent
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = AuditEventSources.Management,
            TenantId = httpContext.GetOpenIdContext().Tenant.TenantId,
            Actor = GetActor(httpContext),
            Request = CreateRequest(httpContext),
            Outcome = AuditOutcome.Success,
            ResourceId = OpenIdConstants.PlatformResourceId,
            ChangeType = changeType,
            ResourceValues = resourceValues,
        };

        return RecordAsync(httpContext, null, auditEvent);
    }

    /// <inheritdoc />
    public ValueTask RecordResourceServerChangedAsync(
        HttpContext httpContext,
        string tenantId,
        string resourceServerId,
        string changeType,
        JsonElement? resourceValues
    )
    {
        var now = TimeProvider.GetUtcNow();
        var auditEvent = new ResourceServerChangedAuditEvent
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = AuditEventSources.Management,
            TenantId = tenantId,
            Actor = GetActor(httpContext),
            Request = CreateRequest(httpContext),
            Outcome = AuditOutcome.Success,
            ResourceId = resourceServerId,
            ChangeType = changeType,
            ResourceValues = resourceValues,
        };

        return RecordAsync(httpContext, tenantId, auditEvent);
    }

    /// <inheritdoc />
    public ValueTask RecordResourceServerScopeChangedAsync(
        HttpContext httpContext,
        string tenantId,
        string resourceServerId,
        string scopeValue,
        string changeType,
        JsonElement? resourceValues
    )
    {
        var now = TimeProvider.GetUtcNow();
        var auditEvent = new ResourceServerScopeChangedAuditEvent
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = AuditEventSources.Management,
            TenantId = tenantId,
            Actor = GetActor(httpContext),
            Request = CreateRequest(httpContext),
            Outcome = AuditOutcome.Success,
            ResourceServerId = resourceServerId,
            ResourceId = scopeValue,
            ChangeType = changeType,
            ResourceValues = resourceValues,
        };

        return RecordAsync(httpContext, tenantId, auditEvent);
    }

    /// <inheritdoc />
    public ValueTask RecordResourceServerSecretChangedAsync(
        HttpContext httpContext,
        string tenantId,
        string resourceServerId,
        string secretId,
        string changeType,
        JsonElement? resourceValues
    )
    {
        var now = TimeProvider.GetUtcNow();
        var auditEvent = new ResourceServerSecretChangedAuditEvent
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = AuditEventSources.Management,
            TenantId = tenantId,
            Actor = GetActor(httpContext),
            Request = CreateRequest(httpContext),
            Outcome = AuditOutcome.Success,
            ResourceServerId = resourceServerId,
            ResourceId = secretId,
            ChangeType = changeType,
            ResourceValues = resourceValues,
        };

        return RecordAsync(httpContext, tenantId, auditEvent);
    }

    /// <inheritdoc />
    public ValueTask RecordResourceChangeAsync(
        HttpContext httpContext,
        ResourceChangeAuditEvent auditEvent
    )
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        // Stamp the common envelope (the caller owns the resource-specific fields and outcome); `with` preserves the
        // runtime event type so dispatch keys off the concrete event.
        var now = TimeProvider.GetUtcNow();
        var stamped = auditEvent with
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = AuditEventSources.Management,
            Actor = GetActor(httpContext),
            Request = CreateRequest(httpContext),
        };

        var environmentTenantId = auditEvent.TenantEnvironmentType is null
            ? auditEvent.TenantId
            : null;
        return RecordAsync(httpContext, environmentTenantId, stamped);
    }

    /// <inheritdoc />
    public ValueTask RecordMasterKeyRotatedAsync(
        HttpContext httpContext,
        MasterKeyRotation rotation,
        string reason
    )
    {
        var now = TimeProvider.GetUtcNow();
        var owner = rotation.Owner;
        var tenantId = owner.TenantId;

        // A platform event is recorded in the control-plane tenant, as every platform event is.
        var recordedTenantId = string.Equals(
            owner.OwnerType,
            SecretOwnerTypes.Platform,
            StringComparison.OrdinalIgnoreCase
        )
            ? httpContext.GetOpenIdContext().Tenant.TenantId
            : tenantId;

        var auditEvent = new MasterKeyAuditEvent
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = AuditEventSources.Management,
            TenantId = recordedTenantId,
            Actor = GetActor(httpContext),
            Request = CreateRequest(httpContext),
            Outcome = AuditOutcome.Success,
            Change = MasterKeyChanges.Rotated,
            Reason = reason,
            Key = rotation.ActiveKey,
            RetiredKey = rotation.RetiredKey,
        };

        return RecordAsync(httpContext, tenantId, auditEvent);
    }

    /// <inheritdoc />
    public ValueTask RecordRootKeyChangedAsync(
        HttpContext httpContext,
        OwnerRootKeyInfo rootKey,
        string change,
        string reason
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
        auditEvent = auditEvent with { Request = CreateRequest(httpContext) };

        return RecordAsync(httpContext, tenantId, auditEvent);
    }

    // The record — the environment lookup and the publish — runs once the change is committed, on one bounded token of
    // its own, so the request's cancellation never drops the audit record. A failure, its timeout included, is logged
    // and never turns a committed change into an error response.
    private async ValueTask RecordAsync<TEvent>(
        HttpContext httpContext,
        string? environmentTenantId,
        TEvent auditEvent
    )
        where TEvent : AuditEvent
    {
        try
        {
            await PostCommitWork.RunAsync(
                TimeProvider,
                static async (state, cancellationToken) =>
                {
                    var stamped = state.AuditEvent;
                    if (state.TenantId is not null)
                    {
                        var environmentType =
                            await state.Recorder.TenantEnvironmentResolver.GetEnvironmentTypeAsync(
                                state.HttpContext,
                                state.TenantId,
                                cancellationToken
                            );
                        stamped = (TEvent)(
                            ((AuditEvent)stamped) with
                            {
                                TenantEnvironmentType = environmentType,
                            }
                        );
                    }

                    await state.Recorder.EventPublisher.PublishAsync(stamped, cancellationToken);
                },
                (
                    Recorder: this,
                    HttpContext: httpContext,
                    TenantId: environmentTenantId,
                    AuditEvent: auditEvent
                )
            );
        }
        catch (Exception exception)
        {
            Logger.AuditPublishFailed(exception, auditEvent.GetType().Name);
        }
    }

    private static AuditRequest CreateRequest(HttpContext httpContext) =>
        AuditRequestFactory.Create(httpContext);

    private AuditActor GetActor(HttpContext httpContext)
    {
        var subjectId = GetActorId(httpContext);
        if (!string.IsNullOrEmpty(subjectId))
        {
            return AuditActor.Administrator(subjectId);
        }

        // A client acting for itself is the actor of the changes it makes.
        // See ADR-1419.
        var clientId = httpContext.User.FindFirstValue(JoseClaimNames.Payload.ClientId);
        return string.IsNullOrEmpty(clientId)
            ? AuditActor.Administrator(subjectId)
            : AuditActor.Client(clientId);
    }

    // The actor is a self-issued/control-plane admin, so its subject is read with
    // OpenIdConstants.DefaultSubjectClaimTypes (ADR-1053) through the app-level environment — not the request tenant's
    // subject_claim_types setting.
    private string? GetActorId(HttpContext httpContext) =>
        EnvironmentProvider
            .Get()
            .GetSubjectId(httpContext.User, OpenIdConstants.DefaultSubjectClaimTypes);
}
