// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NCode.Identity.Events;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Management.Logging;
using NCode.Identity.Secrets.Persistence.Auditing;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.OpenId.Management.Auditing;

public sealed class DefaultManagementAuditRecorderTests : IDisposable
{
    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);

    private const string TenantEnvironmentType = "staging";

    public void Dispose() => MockRepository.Verify();

    private static HttpContext CreateHttpContext(string? actorId)
    {
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "trace-1",
            User = actorId is null
                ? new ClaimsPrincipal(new ClaimsIdentity())
                : new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", actorId)])),
        };

        httpContext.SetEndpoint(
            new Endpoint(
                requestDelegate: null,
                new EndpointMetadataCollection(new EndpointNameMetadata("test-endpoint")),
                displayName: "Test display name"
            )
        );

        return httpContext;
    }

    // The actor id is derived through the host-configured environment seam; mirror the default `sub` extraction.
    // A change in a tenant whose event carries no environment type resolves the tenant's; a server change or an event
    // already stamped with one does not.
    private DefaultManagementAuditRecorder CreateRecorder(
        IEventPublisher eventPublisher,
        bool resolvesTenantEnvironment = true,
        ILogger<DefaultManagementAuditRecorder>? logger = null
    )
    {
        var mockEnvironment = MockRepository.Create<OpenIdEnvironment>();
        mockEnvironment
            .Setup(x =>
                x.GetSubjectId(It.IsAny<ClaimsPrincipal>(), It.IsAny<IReadOnlyCollection<string>>())
            )
            .Returns(
                (ClaimsPrincipal principal, IReadOnlyCollection<string> _) =>
                    principal.FindFirst("sub")?.Value
            )
            .Verifiable();

        var mockEnvironmentProvider = MockRepository.Create<IOpenIdEnvironmentProvider>();
        mockEnvironmentProvider.Setup(x => x.Get()).Returns(mockEnvironment.Object).Verifiable();

        var mockTenantEnvironmentResolver =
            MockRepository.Create<IManagementTenantEnvironmentResolver>();
        if (resolvesTenantEnvironment)
        {
            mockTenantEnvironmentResolver
                .Setup(x =>
                    x.GetEnvironmentTypeAsync(
                        It.IsAny<HttpContext>(),
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(TenantEnvironmentType)
                .Verifiable();
        }

        return new DefaultManagementAuditRecorder(
            eventPublisher,
            mockEnvironmentProvider.Object,
            mockTenantEnvironmentResolver.Object,
            TimeProvider.System,
            logger ?? NullLogger<DefaultManagementAuditRecorder>.Instance
        );
    }

    #region PublishAsync Tests

    [Fact]
    public async Task RecordGrantRevokedAsync_WhenThePublisherThrows_LogsItAndDoesNotThrow()
    {
        var mockPublisher = MockRepository.Create<IEventPublisher>();
        mockPublisher
            .Setup(x =>
                x.PublishAsync(It.IsAny<GrantRevokedAuditEvent>(), It.IsAny<CancellationToken>())
            )
            .Throws(new InvalidOperationException("The publisher failed."))
            .Verifiable();
        var mockLogger = MockRepository.Create<ILogger<DefaultManagementAuditRecorder>>();
        mockLogger.Setup(x => x.IsEnabled(LogLevel.Warning)).Returns(true);
        mockLogger
            .Setup(x =>
                x.Log(
                    LogLevel.Warning,
                    It.Is<EventId>(id => id.Id == EventIds.AuditPublishFailed),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<InvalidOperationException>(),
                    (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()
                )
            )
            .Verifiable();
        var recorder = CreateRecorder(mockPublisher.Object, logger: mockLogger.Object);

        await recorder.RecordGrantRevokedAsync(
            CreateHttpContext("admin-1"),
            "tenant-1",
            "subject-1",
            "client-1",
            "authorization_code",
            CancellationToken.None
        );
    }

    [Fact]
    public async Task RecordGrantRevokedAsync_WhenTheRequestIsCancelled_PublishesOnATokenThatIsNotTheRequests()
    {
        var mockPublisher = MockRepository.Create<IEventPublisher>();
        var publishToken = CancellationToken.None;
        mockPublisher
            .Setup(x =>
                x.PublishAsync(It.IsAny<GrantRevokedAuditEvent>(), It.IsAny<CancellationToken>())
            )
            .Callback((GrantRevokedAuditEvent _, CancellationToken token) => publishToken = token)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        var recorder = CreateRecorder(mockPublisher.Object);
        using var request = new CancellationTokenSource();
        await request.CancelAsync();

        await recorder.RecordGrantRevokedAsync(
            CreateHttpContext("admin-1"),
            "tenant-1",
            "subject-1",
            "client-1",
            "authorization_code",
            request.Token
        );

        Assert.NotEqual(request.Token, publishToken);
        Assert.True(publishToken.CanBeCanceled);
        Assert.False(publishToken.IsCancellationRequested);
    }

    #endregion

    #region RecordGrantRevokedAsync Tests

    [Fact]
    public async Task RecordGrantRevokedAsync_PublishesGrantRevokedAuditEventWithMetadata()
    {
        var mockPublisher = MockRepository.Create<IEventPublisher>();

        GrantRevokedAuditEvent? captured = null;
        mockPublisher
            .Setup(x =>
                x.PublishAsync(It.IsAny<GrantRevokedAuditEvent>(), It.IsAny<CancellationToken>())
            )
            .Callback(
                (GrantRevokedAuditEvent auditEvent, CancellationToken _) => captured = auditEvent
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var recorder = CreateRecorder(mockPublisher.Object);

        var httpContext = CreateHttpContext("admin-1");

        await recorder.RecordGrantRevokedAsync(
            httpContext,
            "tenant-1",
            "subject-1",
            "client-1",
            "authorization_code",
            CancellationToken.None
        );

        Assert.NotNull(captured);
        Assert.Equal("grant.revoked", captured.EventName);
        Assert.Equal(AuditEventSources.Management, captured.EventSource);
        Assert.Equal(AuditOutcome.Success, captured.Outcome);
        Assert.Equal("tenant-1", captured.TenantId);
        Assert.Equal(TenantEnvironmentType, captured.TenantEnvironmentType);
        Assert.Equal("subject-1", captured.SubjectId);
        Assert.Equal("client-1", captured.ClientId);
        Assert.Equal("authorization_code", captured.GrantType);
        Assert.Equal(AuditActorTypes.Administrator, captured.Actor.Type);
        Assert.Equal("admin-1", captured.Actor.Id);
        Assert.NotNull(captured.Request);
        Assert.Equal("trace-1", captured.Request.RequestId);
        Assert.Equal("test-endpoint", captured.Request.Endpoint);
        Assert.Equal(7, captured.EventId.Version);
    }

    #endregion

    #region RecordMasterKeyRotatedAsync Tests

    private static MasterKeyInfo KeyInfo(SecretOwner owner, int version, bool retired) =>
        new(
            Guid.NewGuid(),
            owner,
            version,
            DateTimeOffset.UnixEpoch,
            retired ? DateTimeOffset.UnixEpoch.AddDays(1) : null,
            "local",
            "root-key",
            null,
            null
        );

    [Fact]
    public async Task RecordMasterKeyRotatedAsync_ATenantRekey_PublishesTheEventWithTheAdministratorAndEnvironment()
    {
        var mockPublisher = MockRepository.Create<IEventPublisher>();
        MasterKeyAuditEvent? captured = null;
        mockPublisher
            .Setup(x =>
                x.PublishAsync(It.IsAny<MasterKeyAuditEvent>(), It.IsAny<CancellationToken>())
            )
            .Callback(
                (MasterKeyAuditEvent auditEvent, CancellationToken _) => captured = auditEvent
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        var owner = SecretOwner.ForTenant("tenant-1");
        var rotation = new MasterKeyRotation(
            owner,
            KeyInfo(owner, 2, retired: false),
            KeyInfo(owner, 1, retired: true)
        );

        await CreateRecorder(mockPublisher.Object)
            .RecordMasterKeyRotatedAsync(
                CreateHttpContext("admin-1"),
                rotation,
                MasterKeyChangeReasons.Requested,
                CancellationToken.None
            );

        Assert.NotNull(captured);
        Assert.Equal("master_key.rotated", captured.EventName);
        Assert.Equal(AuditEventSources.Management, captured.EventSource);
        Assert.Equal("requested", captured.Reason);
        Assert.Equal("tenant-1", captured.TenantId);
        Assert.Equal(TenantEnvironmentType, captured.TenantEnvironmentType);
        Assert.Equal(AuditActorTypes.Administrator, captured.Actor.Type);
        Assert.Equal("admin-1", captured.Actor.Id);
        Assert.Equal(rotation.ActiveKey.KeyId, captured.KeyId);
        Assert.Equal(rotation.RetiredKey?.KeyId, captured.RetiredKeyId);
        Assert.Equal(1, captured.RetiredVersion);
        Assert.Equal(7, captured.EventId.Version);
    }

    [Fact]
    public async Task RecordMasterKeyRotatedAsync_ARootKeyRotation_CarriesTheReasonAndTheRootKeyReference()
    {
        var mockPublisher = MockRepository.Create<IEventPublisher>();
        MasterKeyAuditEvent? captured = null;
        mockPublisher
            .Setup(x =>
                x.PublishAsync(It.IsAny<MasterKeyAuditEvent>(), It.IsAny<CancellationToken>())
            )
            .Callback(
                (MasterKeyAuditEvent auditEvent, CancellationToken _) => captured = auditEvent
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        var owner = SecretOwner.ForTenant("tenant-1");
        var rotation = new MasterKeyRotation(
            owner,
            KeyInfo(owner, 2, retired: false) with
            {
                RootKeyReference = "kms://tenant-1/root",
            },
            null
        );

        await CreateRecorder(mockPublisher.Object)
            .RecordMasterKeyRotatedAsync(
                CreateHttpContext("admin-1"),
                rotation,
                MasterKeyChangeReasons.RootKeyChanged,
                CancellationToken.None
            );

        Assert.NotNull(captured);
        Assert.Equal("root_key_changed", captured.Reason);
        Assert.Equal("kms://tenant-1/root", captured.RootKeyReference);
    }

    [Theory]
    [InlineData(RootKeyChanges.Set, OwnerRootKeyStates.Active, "success", null)]
    [InlineData(RootKeyChanges.Removed, OwnerRootKeyStates.Removing, "success", null)]
    [InlineData(RootKeyChanges.Restored, OwnerRootKeyStates.Active, "success", null)]
    [InlineData(
        RootKeyChanges.Unavailable,
        OwnerRootKeyStates.Unavailable,
        "failure",
        "AccessDenied"
    )]
    public async Task RecordRootKeyChangedAsync_ATenantChange_PublishesTheEventWithTheAdministratorAndEnvironment(
        string change,
        string state,
        string expectedOutcome,
        string? failure
    )
    {
        var mockPublisher = MockRepository.Create<IEventPublisher>();
        RootKeyAuditEvent? captured = null;
        mockPublisher
            .Setup(x =>
                x.PublishAsync(It.IsAny<RootKeyAuditEvent>(), It.IsAny<CancellationToken>())
            )
            .Callback((RootKeyAuditEvent auditEvent, CancellationToken _) => captured = auditEvent)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        var rootKey = new OwnerRootKeyInfo(
            SecretOwner.ForTenant("tenant-1"),
            "aws-kms",
            "kms://tenant-1/root",
            state,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            null,
            null,
            failure
        );

        await CreateRecorder(mockPublisher.Object)
            .RecordRootKeyChangedAsync(
                CreateHttpContext("admin-1"),
                rootKey,
                change,
                RootKeyChangeReasons.Requested,
                CancellationToken.None
            );

        Assert.NotNull(captured);
        Assert.Equal($"root_key.{change}", captured.EventName);
        Assert.Equal(AuditEventSources.Management, captured.EventSource);
        Assert.Equal(expectedOutcome, captured.Outcome);
        Assert.Equal("tenant-1", captured.TenantId);
        Assert.Equal(TenantEnvironmentType, captured.TenantEnvironmentType);
        Assert.Equal("admin-1", captured.Actor.Id);
        Assert.Equal("kms://tenant-1/root", captured.KeyReference);
        Assert.Equal("aws-kms", captured.KeyWrapperName);
        Assert.Equal(state, captured.State);
        Assert.Equal(failure, captured.Failure);
        Assert.Equal(7, captured.EventId.Version);
    }

    #endregion

    #region RecordGrantsRevokedAsync Tests

    [Fact]
    public async Task RecordGrantsRevokedAsync_PublishesBulkAuditEventWithCount()
    {
        var mockPublisher = MockRepository.Create<IEventPublisher>();

        GrantsRevokedBulkAuditEvent? captured = null;
        mockPublisher
            .Setup(x =>
                x.PublishAsync(
                    It.IsAny<GrantsRevokedBulkAuditEvent>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (GrantsRevokedBulkAuditEvent auditEvent, CancellationToken _) =>
                    captured = auditEvent
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var recorder = CreateRecorder(mockPublisher.Object);

        var httpContext = CreateHttpContext("admin-1");

        await recorder.RecordGrantsRevokedAsync(
            httpContext,
            "tenant-1",
            "subject-1",
            clientId: null,
            sessionId: "sid-1",
            revokedCount: 7,
            CancellationToken.None
        );

        Assert.NotNull(captured);
        Assert.Equal("grant.revoked.bulk", captured.EventName);
        Assert.Equal(AuditEventSources.Management, captured.EventSource);
        Assert.Equal(AuditOutcome.Success, captured.Outcome);
        Assert.Equal("tenant-1", captured.TenantId);
        Assert.Equal(TenantEnvironmentType, captured.TenantEnvironmentType);
        Assert.Equal("subject-1", captured.SubjectId);
        Assert.Null(captured.ClientId);
        Assert.Equal("sid-1", captured.SessionId);
        Assert.Equal(7, captured.RevokedCount);
        Assert.Equal(AuditActorTypes.Administrator, captured.Actor.Type);
        Assert.Equal("admin-1", captured.Actor.Id);
        Assert.NotNull(captured.Request);
        Assert.Equal("trace-1", captured.Request.RequestId);
        Assert.Equal("test-endpoint", captured.Request.Endpoint);
        Assert.Equal(7, captured.EventId.Version);
    }

    #endregion

    #region RecordSessionsRevokedAsync Tests

    [Fact]
    public async Task RecordSessionsRevokedAsync_PublishesAuditEventWithCount()
    {
        var mockPublisher = MockRepository.Create<IEventPublisher>();

        SessionsRevokedAuditEvent? captured = null;
        mockPublisher
            .Setup(x =>
                x.PublishAsync(It.IsAny<SessionsRevokedAuditEvent>(), It.IsAny<CancellationToken>())
            )
            .Callback(
                (SessionsRevokedAuditEvent auditEvent, CancellationToken _) => captured = auditEvent
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var recorder = CreateRecorder(mockPublisher.Object);

        var httpContext = CreateHttpContext("admin-1");

        await recorder.RecordSessionsRevokedAsync(
            httpContext,
            "tenant-1",
            "subject-1",
            sessionId: "sid-1",
            revokedCount: 1,
            CancellationToken.None
        );

        Assert.NotNull(captured);
        Assert.Equal("session.revoked.admin", captured.EventName);
        Assert.Equal(AuditEventSources.Management, captured.EventSource);
        Assert.Equal(AuditOutcome.Success, captured.Outcome);
        Assert.Equal("tenant-1", captured.TenantId);
        Assert.Equal(TenantEnvironmentType, captured.TenantEnvironmentType);
        Assert.Equal("subject-1", captured.SubjectId);
        Assert.Equal("sid-1", captured.SessionId);
        Assert.Equal(1, captured.RevokedCount);
        Assert.Equal(AuditActorTypes.Administrator, captured.Actor.Type);
        Assert.Equal("admin-1", captured.Actor.Id);
        Assert.NotNull(captured.Request);
        Assert.Equal("trace-1", captured.Request.RequestId);
        Assert.Equal("test-endpoint", captured.Request.Endpoint);
        Assert.Equal(7, captured.EventId.Version);
    }

    #endregion

    #region RecordClientChangedAsync Tests

    [Fact]
    public async Task RecordClientChangedAsync_PublishesClientChangedAuditEventWithSnapshot()
    {
        var mockPublisher = MockRepository.Create<IEventPublisher>();

        ClientChangedAuditEvent? captured = null;
        mockPublisher
            .Setup(x =>
                x.PublishAsync(It.IsAny<ClientChangedAuditEvent>(), It.IsAny<CancellationToken>())
            )
            .Callback(
                (ClientChangedAuditEvent auditEvent, CancellationToken _) => captured = auditEvent
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var recorder = CreateRecorder(mockPublisher.Object);

        var httpContext = CreateHttpContext("admin-1");
        var resourceValues = JsonSerializer.SerializeToElement(new { isDisabled = false });

        await recorder.RecordClientChangedAsync(
            httpContext,
            "tenant-1",
            "client-1",
            ResourceChangeTypes.Created,
            resourceValues,
            CancellationToken.None
        );

        Assert.NotNull(captured);
        Assert.Equal("client.created", captured.EventName);
        Assert.Equal(AuditEventSources.Management, captured.EventSource);
        Assert.Equal(ManagementResourceTypes.Client, captured.ResourceType);
        Assert.Equal(ResourceChangeTypes.Created, captured.ChangeType);
        Assert.Equal(AuditOutcome.Success, captured.Outcome);
        Assert.Equal("tenant-1", captured.TenantId);
        Assert.Equal(TenantEnvironmentType, captured.TenantEnvironmentType);
        Assert.Equal("client-1", captured.ClientId);
        Assert.Equal("client-1", captured.ResourceId);
        Assert.Equal(AuditActorTypes.Administrator, captured.Actor.Type);
        Assert.Equal("admin-1", captured.Actor.Id);
        Assert.NotNull(captured.Request);
        Assert.Equal("trace-1", captured.Request.RequestId);
        Assert.Equal("test-endpoint", captured.Request.Endpoint);
        Assert.Equal(7, captured.EventId.Version);
        Assert.Equal(
            [new AuditResource(ManagementResourceTypes.Client, "client-1")],
            captured.Resources
        );
        Assert.NotNull(captured.ResourceValues);
    }

    #endregion

    #region RecordClientSecretChangedAsync Tests

    [Fact]
    public async Task RecordClientSecretChangedAsync_PublishesSecretChangedAuditEventWithIds()
    {
        var mockPublisher = MockRepository.Create<IEventPublisher>();

        ClientSecretChangedAuditEvent? captured = null;
        mockPublisher
            .Setup(x =>
                x.PublishAsync(
                    It.IsAny<ClientSecretChangedAuditEvent>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (ClientSecretChangedAuditEvent auditEvent, CancellationToken _) =>
                    captured = auditEvent
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var recorder = CreateRecorder(mockPublisher.Object);

        var httpContext = CreateHttpContext("admin-1");

        await recorder.RecordClientSecretChangedAsync(
            httpContext,
            "tenant-1",
            "client-1",
            "secret-1",
            ResourceChangeTypes.Deleted,
            resourceValues: null,
            CancellationToken.None
        );

        Assert.NotNull(captured);
        Assert.Equal("client.secret.deleted", captured.EventName);
        Assert.Equal(AuditEventSources.Management, captured.EventSource);
        Assert.Equal(ManagementResourceTypes.ClientSecret, captured.ResourceType);
        Assert.Equal(ResourceChangeTypes.Deleted, captured.ChangeType);
        Assert.Equal(AuditOutcome.Success, captured.Outcome);
        Assert.Equal("tenant-1", captured.TenantId);
        Assert.Equal(TenantEnvironmentType, captured.TenantEnvironmentType);
        Assert.Equal("client-1", captured.ClientId);
        Assert.Equal("secret-1", captured.ResourceId);
        Assert.Equal(AuditActorTypes.Administrator, captured.Actor.Type);
        Assert.Equal("admin-1", captured.Actor.Id);
        Assert.NotNull(captured.Request);
        Assert.Equal("trace-1", captured.Request.RequestId);
        Assert.Equal("test-endpoint", captured.Request.Endpoint);
        Assert.Equal(7, captured.EventId.Version);
        Assert.Equal(
            [
                new AuditResource(ManagementResourceTypes.ClientSecret, "secret-1"),
                new AuditResource(ManagementResourceTypes.Client, "client-1"),
            ],
            captured.Resources
        );
        Assert.Null(captured.ResourceValues);
    }

    #endregion

    #region RecordTenantChangedAsync Tests

    [Fact]
    public async Task RecordTenantChangedAsync_PublishesTenantChangedAuditEventWithSnapshot()
    {
        var mockPublisher = MockRepository.Create<IEventPublisher>();

        TenantChangedAuditEvent? captured = null;
        mockPublisher
            .Setup(x =>
                x.PublishAsync(It.IsAny<TenantChangedAuditEvent>(), It.IsAny<CancellationToken>())
            )
            .Callback(
                (TenantChangedAuditEvent auditEvent, CancellationToken _) => captured = auditEvent
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var recorder = CreateRecorder(mockPublisher.Object);

        var httpContext = CreateHttpContext("admin-1");
        var resourceValues = JsonSerializer.SerializeToElement(new { isDisabled = false });

        await recorder.RecordTenantChangedAsync(
            httpContext,
            "tenant-1",
            ResourceChangeTypes.Updated,
            resourceValues,
            CancellationToken.None
        );

        Assert.NotNull(captured);
        Assert.Equal("tenant.updated", captured.EventName);
        Assert.Equal(AuditEventSources.Management, captured.EventSource);
        Assert.Equal(ManagementResourceTypes.Tenant, captured.ResourceType);
        Assert.Equal(ResourceChangeTypes.Updated, captured.ChangeType);
        Assert.Equal(AuditOutcome.Success, captured.Outcome);
        Assert.Equal("tenant-1", captured.TenantId);
        Assert.Equal(TenantEnvironmentType, captured.TenantEnvironmentType);
        Assert.Equal("tenant-1", captured.ResourceId);
        Assert.Equal(AuditActorTypes.Administrator, captured.Actor.Type);
        Assert.Equal("admin-1", captured.Actor.Id);
        Assert.NotNull(captured.Request);
        Assert.Equal("trace-1", captured.Request.RequestId);
        Assert.Equal("test-endpoint", captured.Request.Endpoint);
        Assert.Equal(7, captured.EventId.Version);
        Assert.Equal(
            [new AuditResource(ManagementResourceTypes.Tenant, "tenant-1")],
            captured.Resources
        );
        Assert.NotNull(captured.ResourceValues);
    }

    #endregion

    #region RecordTenantSecretChangedAsync Tests

    [Fact]
    public async Task RecordTenantSecretChangedAsync_PublishesSecretChangedAuditEventWithIds()
    {
        var mockPublisher = MockRepository.Create<IEventPublisher>();

        TenantSecretChangedAuditEvent? captured = null;
        mockPublisher
            .Setup(x =>
                x.PublishAsync(
                    It.IsAny<TenantSecretChangedAuditEvent>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (TenantSecretChangedAuditEvent auditEvent, CancellationToken _) =>
                    captured = auditEvent
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var recorder = CreateRecorder(mockPublisher.Object);

        var httpContext = CreateHttpContext("admin-1");

        await recorder.RecordTenantSecretChangedAsync(
            httpContext,
            "tenant-1",
            "secret-1",
            ResourceChangeTypes.Created,
            resourceValues: null,
            CancellationToken.None
        );

        Assert.NotNull(captured);
        Assert.Equal("tenant.secret.created", captured.EventName);
        Assert.Equal(AuditEventSources.Management, captured.EventSource);
        Assert.Equal(ManagementResourceTypes.TenantSecret, captured.ResourceType);
        Assert.Equal(ResourceChangeTypes.Created, captured.ChangeType);
        Assert.Equal(AuditOutcome.Success, captured.Outcome);
        Assert.Equal("tenant-1", captured.TenantId);
        Assert.Equal(TenantEnvironmentType, captured.TenantEnvironmentType);
        Assert.Equal("secret-1", captured.ResourceId);
        Assert.Equal(AuditActorTypes.Administrator, captured.Actor.Type);
        Assert.Equal("admin-1", captured.Actor.Id);
        Assert.NotNull(captured.Request);
        Assert.Equal("trace-1", captured.Request.RequestId);
        Assert.Equal("test-endpoint", captured.Request.Endpoint);
        Assert.Equal(7, captured.EventId.Version);
        Assert.Equal(
            [
                new AuditResource(ManagementResourceTypes.TenantSecret, "secret-1"),
                new AuditResource(ManagementResourceTypes.Tenant, "tenant-1"),
            ],
            captured.Resources
        );
        Assert.Null(captured.ResourceValues);
    }

    #endregion

    #region RecordServerChangedAsync Tests

    [Fact]
    public async Task RecordServerChangedAsync_PublishesServerChangedAuditEventWithSnapshot()
    {
        var mockPublisher = MockRepository.Create<IEventPublisher>();

        ServerChangedAuditEvent? captured = null;
        mockPublisher
            .Setup(x =>
                x.PublishAsync(It.IsAny<ServerChangedAuditEvent>(), It.IsAny<CancellationToken>())
            )
            .Callback(
                (ServerChangedAuditEvent auditEvent, CancellationToken _) => captured = auditEvent
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var recorder = CreateRecorder(mockPublisher.Object, resolvesTenantEnvironment: false);

        var httpContext = CreateHttpContext("admin-1");
        var resourceValues = JsonSerializer.SerializeToElement(new { displayName = "My Server" });

        await recorder.RecordServerChangedAsync(
            httpContext,
            "server-1",
            ResourceChangeTypes.Updated,
            resourceValues,
            CancellationToken.None
        );

        Assert.NotNull(captured);
        Assert.Equal("server.updated", captured.EventName);
        Assert.Equal(AuditEventSources.Management, captured.EventSource);
        Assert.Equal(ManagementResourceTypes.Server, captured.ResourceType);
        Assert.Equal(ResourceChangeTypes.Updated, captured.ChangeType);
        Assert.Equal(AuditOutcome.Success, captured.Outcome);
        Assert.Null(captured.TenantId);
        Assert.Null(captured.TenantEnvironmentType);
        Assert.Equal("server-1", captured.ResourceId);
        Assert.Equal(AuditActorTypes.Administrator, captured.Actor.Type);
        Assert.Equal("admin-1", captured.Actor.Id);
        Assert.NotNull(captured.Request);
        Assert.Equal("trace-1", captured.Request.RequestId);
        Assert.Equal("test-endpoint", captured.Request.Endpoint);
        Assert.Equal(7, captured.EventId.Version);
        Assert.Equal(
            [new AuditResource(ManagementResourceTypes.Server, "server-1")],
            captured.Resources
        );
        Assert.NotNull(captured.ResourceValues);
    }

    #endregion

    #region RecordServerSecretChangedAsync Tests

    [Fact]
    public async Task RecordServerSecretChangedAsync_PublishesSecretChangedAuditEventWithIds()
    {
        var mockPublisher = MockRepository.Create<IEventPublisher>();

        ServerSecretChangedAuditEvent? captured = null;
        mockPublisher
            .Setup(x =>
                x.PublishAsync(
                    It.IsAny<ServerSecretChangedAuditEvent>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (ServerSecretChangedAuditEvent auditEvent, CancellationToken _) =>
                    captured = auditEvent
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var recorder = CreateRecorder(mockPublisher.Object, resolvesTenantEnvironment: false);

        var httpContext = CreateHttpContext("admin-1");

        await recorder.RecordServerSecretChangedAsync(
            httpContext,
            "server-1",
            "secret-1",
            ResourceChangeTypes.Created,
            resourceValues: null,
            CancellationToken.None
        );

        Assert.NotNull(captured);
        Assert.Equal("server.secret.created", captured.EventName);
        Assert.Equal(AuditEventSources.Management, captured.EventSource);
        Assert.Equal(ManagementResourceTypes.ServerSecret, captured.ResourceType);
        Assert.Equal(ResourceChangeTypes.Created, captured.ChangeType);
        Assert.Equal(AuditOutcome.Success, captured.Outcome);
        Assert.Null(captured.TenantId);
        Assert.Null(captured.TenantEnvironmentType);
        Assert.Equal("server-1", captured.ServerId);
        Assert.Equal("secret-1", captured.ResourceId);
        Assert.Equal(AuditActorTypes.Administrator, captured.Actor.Type);
        Assert.Equal("admin-1", captured.Actor.Id);
        Assert.NotNull(captured.Request);
        Assert.Equal("trace-1", captured.Request.RequestId);
        Assert.Equal("test-endpoint", captured.Request.Endpoint);
        Assert.Equal(7, captured.EventId.Version);
        Assert.Equal(
            [
                new AuditResource(ManagementResourceTypes.ServerSecret, "secret-1"),
                new AuditResource(ManagementResourceTypes.Server, "server-1"),
            ],
            captured.Resources
        );
        Assert.Null(captured.ResourceValues);
    }

    #endregion

    #region RecordResourceServerChangedAsync Tests

    [Fact]
    public async Task RecordResourceServerChangedAsync_PublishesResourceServerChangedAuditEventWithSnapshot()
    {
        var mockPublisher = MockRepository.Create<IEventPublisher>();

        ResourceServerChangedAuditEvent? captured = null;
        mockPublisher
            .Setup(x =>
                x.PublishAsync(
                    It.IsAny<ResourceServerChangedAuditEvent>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (ResourceServerChangedAuditEvent auditEvent, CancellationToken _) =>
                    captured = auditEvent
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var recorder = CreateRecorder(mockPublisher.Object);

        var httpContext = CreateHttpContext("admin-1");
        var resourceValues = JsonSerializer.SerializeToElement(new { displayName = "My API" });

        await recorder.RecordResourceServerChangedAsync(
            httpContext,
            "tenant-1",
            "resource-server-1",
            ResourceChangeTypes.Updated,
            resourceValues,
            CancellationToken.None
        );

        Assert.NotNull(captured);
        Assert.Equal("resource_server.updated", captured.EventName);
        Assert.Equal(AuditEventSources.Management, captured.EventSource);
        Assert.Equal(ManagementResourceTypes.ResourceServer, captured.ResourceType);
        Assert.Equal(ResourceChangeTypes.Updated, captured.ChangeType);
        Assert.Equal(AuditOutcome.Success, captured.Outcome);
        Assert.Equal("tenant-1", captured.TenantId);
        Assert.Equal(TenantEnvironmentType, captured.TenantEnvironmentType);
        Assert.Equal("resource-server-1", captured.ResourceId);
        Assert.Equal(AuditActorTypes.Administrator, captured.Actor.Type);
        Assert.Equal("admin-1", captured.Actor.Id);
        Assert.NotNull(captured.Request);
        Assert.Equal("trace-1", captured.Request.RequestId);
        Assert.Equal("test-endpoint", captured.Request.Endpoint);
        Assert.Equal(7, captured.EventId.Version);
        Assert.Equal(
            [new AuditResource(ManagementResourceTypes.ResourceServer, "resource-server-1")],
            captured.Resources
        );
        Assert.NotNull(captured.ResourceValues);
    }

    #endregion

    #region RecordResourceServerScopeChangedAsync Tests

    [Fact]
    public async Task RecordResourceServerScopeChangedAsync_PublishesScopeChangedAuditEventWithIds()
    {
        var mockPublisher = MockRepository.Create<IEventPublisher>();

        ResourceServerScopeChangedAuditEvent? captured = null;
        mockPublisher
            .Setup(x =>
                x.PublishAsync(
                    It.IsAny<ResourceServerScopeChangedAuditEvent>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (ResourceServerScopeChangedAuditEvent auditEvent, CancellationToken _) =>
                    captured = auditEvent
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var recorder = CreateRecorder(mockPublisher.Object);

        var httpContext = CreateHttpContext("admin-1");

        await recorder.RecordResourceServerScopeChangedAsync(
            httpContext,
            "tenant-1",
            "resource-server-1",
            "read:things",
            ResourceChangeTypes.Created,
            resourceValues: null,
            CancellationToken.None
        );

        Assert.NotNull(captured);
        Assert.Equal(AuditEventSources.Management, captured.EventSource);
        Assert.Equal(ManagementResourceTypes.ResourceServerScope, captured.ResourceType);
        Assert.Equal(ResourceChangeTypes.Created, captured.ChangeType);
        Assert.Equal(AuditOutcome.Success, captured.Outcome);
        Assert.Equal("tenant-1", captured.TenantId);
        Assert.Equal(TenantEnvironmentType, captured.TenantEnvironmentType);
        Assert.Equal("resource-server-1", captured.ResourceServerId);
        Assert.Equal("read:things", captured.ResourceId);
        Assert.Equal(AuditActorTypes.Administrator, captured.Actor.Type);
        Assert.Equal("admin-1", captured.Actor.Id);
        Assert.NotNull(captured.Request);
        Assert.Equal("trace-1", captured.Request.RequestId);
        Assert.Equal("test-endpoint", captured.Request.Endpoint);
        Assert.Equal(7, captured.EventId.Version);
        Assert.Equal(
            [
                new AuditResource(ManagementResourceTypes.ResourceServerScope, "read:things"),
                new AuditResource(ManagementResourceTypes.ResourceServer, "resource-server-1"),
            ],
            captured.Resources
        );
        Assert.Null(captured.ResourceValues);
    }

    #endregion

    #region RecordResourceServerSecretChangedAsync Tests

    [Fact]
    public async Task RecordResourceServerSecretChangedAsync_PublishesSecretChangedAuditEventWithIds()
    {
        var mockPublisher = MockRepository.Create<IEventPublisher>();

        ResourceServerSecretChangedAuditEvent? captured = null;
        mockPublisher
            .Setup(x =>
                x.PublishAsync(
                    It.IsAny<ResourceServerSecretChangedAuditEvent>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (ResourceServerSecretChangedAuditEvent auditEvent, CancellationToken _) =>
                    captured = auditEvent
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var recorder = CreateRecorder(mockPublisher.Object);

        var httpContext = CreateHttpContext("admin-1");

        await recorder.RecordResourceServerSecretChangedAsync(
            httpContext,
            "tenant-1",
            "resource-server-1",
            "secret-1",
            ResourceChangeTypes.Deleted,
            resourceValues: null,
            CancellationToken.None
        );

        Assert.NotNull(captured);
        Assert.Equal(AuditEventSources.Management, captured.EventSource);
        Assert.Equal(ManagementResourceTypes.ResourceServerSecret, captured.ResourceType);
        Assert.Equal(ResourceChangeTypes.Deleted, captured.ChangeType);
        Assert.Equal(AuditOutcome.Success, captured.Outcome);
        Assert.Equal("tenant-1", captured.TenantId);
        Assert.Equal(TenantEnvironmentType, captured.TenantEnvironmentType);
        Assert.Equal("resource-server-1", captured.ResourceServerId);
        Assert.Equal("secret-1", captured.ResourceId);
        Assert.Equal(AuditActorTypes.Administrator, captured.Actor.Type);
        Assert.Equal("admin-1", captured.Actor.Id);
        Assert.NotNull(captured.Request);
        Assert.Equal("trace-1", captured.Request.RequestId);
        Assert.Equal("test-endpoint", captured.Request.Endpoint);
        Assert.Equal(7, captured.EventId.Version);
        Assert.Equal(
            [
                new AuditResource(ManagementResourceTypes.ResourceServerSecret, "secret-1"),
                new AuditResource(ManagementResourceTypes.ResourceServer, "resource-server-1"),
            ],
            captured.Resources
        );
        Assert.Null(captured.ResourceValues);
    }

    #endregion

    #region RecordResourceChangeAsync Tests

    [Fact]
    public async Task RecordResourceChangeAsync_WithATenant_StampsItsEnvironmentType()
    {
        var mockPublisher = MockRepository.Create<IEventPublisher>();
        ResourceChangeAuditEvent? captured = null;
        mockPublisher
            .Setup(x =>
                x.PublishAsync(It.IsAny<ResourceChangeAuditEvent>(), It.IsAny<CancellationToken>())
            )
            .Callback(
                (ResourceChangeAuditEvent auditEvent, CancellationToken _) => captured = auditEvent
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        var recorder = CreateRecorder(mockPublisher.Object);

        await recorder.RecordResourceChangeAsync(
            CreateHttpContext("admin-1"),
            new PrincipalChangedAuditEvent
            {
                EventSource = AuditEventSources.Management,
                TenantId = "tenant-1",
                Outcome = AuditOutcome.Success,
                ResourceId = "principal-1",
                ChangeType = ResourceChangeTypes.Updated,
            },
            CancellationToken.None
        );

        Assert.NotNull(captured);
        Assert.Equal(TenantEnvironmentType, captured.TenantEnvironmentType);
    }

    [Fact]
    public async Task RecordResourceChangeAsync_WithAStampedEnvironmentType_KeepsIt()
    {
        var mockPublisher = MockRepository.Create<IEventPublisher>();
        ResourceChangeAuditEvent? captured = null;
        mockPublisher
            .Setup(x =>
                x.PublishAsync(It.IsAny<ResourceChangeAuditEvent>(), It.IsAny<CancellationToken>())
            )
            .Callback(
                (ResourceChangeAuditEvent auditEvent, CancellationToken _) => captured = auditEvent
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        var recorder = CreateRecorder(mockPublisher.Object, resolvesTenantEnvironment: false);

        await recorder.RecordResourceChangeAsync(
            CreateHttpContext("admin-1"),
            new PrincipalChangedAuditEvent
            {
                EventSource = AuditEventSources.Management,
                TenantId = "tenant-1",
                TenantEnvironmentType = "development",
                Outcome = AuditOutcome.Success,
                ResourceId = "principal-1",
                ChangeType = ResourceChangeTypes.Updated,
            },
            CancellationToken.None
        );

        Assert.NotNull(captured);
        Assert.Equal("development", captured.TenantEnvironmentType);
    }

    #endregion
}
