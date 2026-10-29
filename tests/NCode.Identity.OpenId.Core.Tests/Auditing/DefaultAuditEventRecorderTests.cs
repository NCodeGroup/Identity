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

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moq;
using NCode.Identity.Events;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Auditing;

public sealed class DefaultAuditEventRecorderTests : IDisposable
{
    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);

    public void Dispose() => MockRepository.Verify();

    private static DefaultHttpContext CreateHttpContext(string endpointName)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.SetEndpoint(
            new Endpoint(
                null,
                new EndpointMetadataCollection(new EndpointNameMetadata(endpointName)),
                endpointName
            )
        );
        return httpContext;
    }

    private void SetupTenantEnvironmentType(Mock<OpenIdTenant> mockTenant, string environmentType)
    {
        var mockSettingsProvider = MockRepository.Create<IReadOnlySettingCollectionProvider>();
        var mockSettings = MockRepository.Create<IReadOnlySettingCollection>();

        mockTenant.Setup(x => x.SettingsProvider).Returns(mockSettingsProvider.Object).Verifiable();
        mockSettingsProvider.Setup(x => x.Collection).Returns(mockSettings.Object).Verifiable();
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.EnvironmentType))
            .Returns(environmentType)
            .Verifiable();
    }

    #region RecordTokenIssuedAsync Tests

    [Fact]
    public async Task RecordTokenIssuedAsync_PublishesTokenIssuedAuditEventWithMetadata()
    {
        var mockContext = MockRepository.Create<OpenIdContext>();
        var mockTenant = MockRepository.Create<OpenIdTenant>();
        var mockClient = MockRepository.Create<OpenIdClient>();
        var mockPublisher = MockRepository.Create<IEventPublisher>();

        mockContext.Setup(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        mockTenant.Setup(x => x.TenantId).Returns("tenant-1").Verifiable();
        SetupTenantEnvironmentType(mockTenant, "staging");
        mockContext.Setup(x => x.Http).Returns(CreateHttpContext("api/token")).Verifiable();
        mockClient.Setup(x => x.ClientId).Returns("client-1").Verifiable();

        TokenIssuedAuditEvent? captured = null;
        mockPublisher
            .Setup(x =>
                x.PublishAsync(It.IsAny<TokenIssuedAuditEvent>(), It.IsAny<CancellationToken>())
            )
            .Callback(
                (TokenIssuedAuditEvent auditEvent, CancellationToken _) => captured = auditEvent
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var recorder = new DefaultAuditEventRecorder(mockPublisher.Object, TimeProvider.System);

        var securityToken = new SecurityToken
        {
            TokenType = "access_token",
            TokenValue = "the-secret-token-value",
            TokenLifetime = default,
        };

        await recorder.RecordTokenIssuedAsync(
            mockContext.Object,
            mockClient.Object,
            "subject-1",
            securityToken,
            CancellationToken.None
        );

        Assert.NotNull(captured);
        Assert.Equal("token.issued", captured.EventName);
        Assert.Equal(AuditEventSources.OpenId, captured.EventSource);
        Assert.Equal(AuditOutcome.Success, captured.Outcome);
        Assert.Equal("access_token", captured.TokenType);
        Assert.Equal("client-1", captured.ClientId);
        Assert.Equal("tenant-1", captured.TenantId);
        Assert.Equal("staging", captured.TenantEnvironmentType);
        Assert.Equal("subject-1", captured.SubjectId);
        Assert.Equal(AuditActor.Client("client-1"), captured.Actor);
        Assert.Equal("api/token", captured.Request?.Endpoint);
        Assert.Equal(7, captured.EventId.Version);
    }

    #endregion

    #region RecordTokenRevokedAsync Tests

    [Fact]
    public async Task RecordTokenRevokedAsync_PublishesTokenRevokedAuditEventWithMetadata()
    {
        var mockContext = MockRepository.Create<OpenIdContext>();
        var mockTenant = MockRepository.Create<OpenIdTenant>();
        var mockClient = MockRepository.Create<OpenIdClient>();
        var mockPublisher = MockRepository.Create<IEventPublisher>();

        mockContext.Setup(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        mockTenant.Setup(x => x.TenantId).Returns("tenant-1").Verifiable();
        SetupTenantEnvironmentType(mockTenant, "production");
        mockContext.Setup(x => x.Http).Returns(CreateHttpContext("api/token")).Verifiable();
        mockClient.Setup(x => x.ClientId).Returns("client-1").Verifiable();

        TokenRevokedAuditEvent? captured = null;
        mockPublisher
            .Setup(x =>
                x.PublishAsync(It.IsAny<TokenRevokedAuditEvent>(), It.IsAny<CancellationToken>())
            )
            .Callback(
                (TokenRevokedAuditEvent auditEvent, CancellationToken _) => captured = auditEvent
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var recorder = new DefaultAuditEventRecorder(mockPublisher.Object, TimeProvider.System);

        await recorder.RecordTokenRevokedAsync(
            mockContext.Object,
            mockClient.Object,
            "subject-1",
            CancellationToken.None
        );

        Assert.NotNull(captured);
        Assert.Equal("token.revoked", captured.EventName);
        Assert.Equal(AuditEventSources.OpenId, captured.EventSource);
        Assert.Equal(AuditOutcome.Success, captured.Outcome);
        Assert.Equal("client-1", captured.ClientId);
        Assert.Equal("tenant-1", captured.TenantId);
        Assert.Equal("production", captured.TenantEnvironmentType);
        Assert.Equal("subject-1", captured.SubjectId);
        Assert.Equal(AuditActor.Client("client-1"), captured.Actor);
        Assert.Equal("api/token", captured.Request?.Endpoint);
        Assert.Equal(7, captured.EventId.Version);
    }

    #endregion

    #region RecordSubjectAuthenticatedAsync Tests

    [Fact]
    public async Task RecordSubjectAuthenticatedAsync_PublishesSuccessAuditEventWithSubject()
    {
        var mockContext = MockRepository.Create<OpenIdContext>();
        var mockTenant = MockRepository.Create<OpenIdTenant>();
        var mockPublisher = MockRepository.Create<IEventPublisher>();

        mockContext.Setup(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        mockTenant.Setup(x => x.TenantId).Returns("tenant-1").Verifiable();
        SetupTenantEnvironmentType(mockTenant, "production");
        mockContext.Setup(x => x.Http).Returns(CreateHttpContext("api/authorize")).Verifiable();

        SubjectAuthenticationAuditEvent? captured = null;
        mockPublisher
            .Setup(x =>
                x.PublishAsync(
                    It.IsAny<SubjectAuthenticationAuditEvent>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (SubjectAuthenticationAuditEvent auditEvent, CancellationToken _) =>
                    captured = auditEvent
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var recorder = new DefaultAuditEventRecorder(mockPublisher.Object, TimeProvider.System);

        await recorder.RecordSubjectAuthenticatedAsync(
            mockContext.Object,
            "subject-1",
            CancellationToken.None
        );

        Assert.NotNull(captured);
        Assert.Equal("subject.authentication", captured.EventName);
        Assert.Equal(AuditEventSources.OpenId, captured.EventSource);
        Assert.Equal(AuditOutcome.Success, captured.Outcome);
        Assert.Equal("tenant-1", captured.TenantId);
        Assert.Equal("production", captured.TenantEnvironmentType);
        Assert.Equal("subject-1", captured.SubjectId);
        Assert.Equal(AuditActor.EndUser("subject-1"), captured.Actor);
        Assert.Equal("api/authorize", captured.Request?.Endpoint);
        Assert.Null(captured.Error);
        Assert.Equal(7, captured.EventId.Version);
    }

    #endregion

    #region RecordSubjectAuthenticationFailedAsync Tests

    [Fact]
    public async Task RecordSubjectAuthenticationFailedAsync_PublishesFailureAuditEventWithError()
    {
        var mockContext = MockRepository.Create<OpenIdContext>();
        var mockTenant = MockRepository.Create<OpenIdTenant>();
        var mockPublisher = MockRepository.Create<IEventPublisher>();

        mockContext.Setup(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        mockTenant.Setup(x => x.TenantId).Returns("tenant-1").Verifiable();
        SetupTenantEnvironmentType(mockTenant, "production");
        mockContext.Setup(x => x.Http).Returns(CreateHttpContext("api/authorize")).Verifiable();

        SubjectAuthenticationAuditEvent? captured = null;
        mockPublisher
            .Setup(x =>
                x.PublishAsync(
                    It.IsAny<SubjectAuthenticationAuditEvent>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (SubjectAuthenticationAuditEvent auditEvent, CancellationToken _) =>
                    captured = auditEvent
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var recorder = new DefaultAuditEventRecorder(mockPublisher.Object, TimeProvider.System);

        await recorder.RecordSubjectAuthenticationFailedAsync(
            mockContext.Object,
            "subject_id_missing",
            "Unable to determine the end-user's subject id.",
            attemptedUsername: "attempted-user",
            CancellationToken.None
        );

        Assert.NotNull(captured);
        Assert.Equal("subject.authentication", captured.EventName);
        Assert.Equal(AuditEventSources.OpenId, captured.EventSource);
        Assert.Equal(AuditOutcome.Failure, captured.Outcome);
        Assert.Equal("tenant-1", captured.TenantId);
        Assert.Equal("production", captured.TenantEnvironmentType);
        Assert.Null(captured.SubjectId);
        Assert.Equal("attempted-user", captured.AttemptedUsername);
        Assert.Equal(AuditActor.Anonymous, captured.Actor);
        Assert.Equal("api/authorize", captured.Request?.Endpoint);
        Assert.Equal("subject_id_missing", captured.Error?.Code);
        Assert.Equal("Unable to determine the end-user's subject id.", captured.Error?.Message);
        Assert.Equal(7, captured.EventId.Version);
    }

    #endregion

    #region Authorization Decision Tests

    [Fact]
    public async Task RecordAuthorizationGrantedAsync_PublishesGrantedAuditEvent()
    {
        var mockContext = MockRepository.Create<OpenIdContext>();
        var mockTenant = MockRepository.Create<OpenIdTenant>();
        var mockClient = MockRepository.Create<OpenIdClient>();
        var mockPublisher = MockRepository.Create<IEventPublisher>();

        mockContext.Setup(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        mockTenant.Setup(x => x.TenantId).Returns("tenant-1").Verifiable();
        SetupTenantEnvironmentType(mockTenant, "production");
        mockContext.Setup(x => x.Http).Returns(CreateHttpContext("api/authorize")).Verifiable();
        mockClient.Setup(x => x.ClientId).Returns("client-1").Verifiable();

        AuthorizationGrantedAuditEvent? captured = null;
        mockPublisher
            .Setup(x =>
                x.PublishAsync(
                    It.IsAny<AuthorizationGrantedAuditEvent>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (AuthorizationGrantedAuditEvent auditEvent, CancellationToken _) =>
                    captured = auditEvent
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var recorder = new DefaultAuditEventRecorder(mockPublisher.Object, TimeProvider.System);

        await recorder.RecordAuthorizationGrantedAsync(
            mockContext.Object,
            mockClient.Object,
            "subject-1",
            CancellationToken.None
        );

        Assert.NotNull(captured);
        Assert.Equal("authorization.granted", captured.EventName);
        Assert.Equal(AuditEventSources.OpenId, captured.EventSource);
        Assert.Equal(AuditOutcome.Success, captured.Outcome);
        Assert.Equal("subject-1", captured.SubjectId);
        Assert.Equal(AuditActor.EndUser("subject-1"), captured.Actor);
        Assert.Equal("api/authorize", captured.Request?.Endpoint);
        Assert.Null(captured.Error);
        Assert.Equal(7, captured.EventId.Version);
    }

    [Fact]
    public async Task RecordAuthorizationDeniedAsync_PublishesDeniedAuditEventWithError()
    {
        var mockContext = MockRepository.Create<OpenIdContext>();
        var mockTenant = MockRepository.Create<OpenIdTenant>();
        var mockClient = MockRepository.Create<OpenIdClient>();
        var mockPublisher = MockRepository.Create<IEventPublisher>();

        mockContext.Setup(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        mockTenant.Setup(x => x.TenantId).Returns("tenant-1").Verifiable();
        SetupTenantEnvironmentType(mockTenant, "production");
        mockContext.Setup(x => x.Http).Returns(CreateHttpContext("api/authorize")).Verifiable();
        mockClient.Setup(x => x.ClientId).Returns("client-1").Verifiable();

        AuthorizationDeniedAuditEvent? captured = null;
        mockPublisher
            .Setup(x =>
                x.PublishAsync(
                    It.IsAny<AuthorizationDeniedAuditEvent>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (AuthorizationDeniedAuditEvent auditEvent, CancellationToken _) =>
                    captured = auditEvent
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var recorder = new DefaultAuditEventRecorder(mockPublisher.Object, TimeProvider.System);

        await recorder.RecordAuthorizationDeniedAsync(
            mockContext.Object,
            mockClient.Object,
            "subject-1",
            "login_required",
            CancellationToken.None
        );

        Assert.NotNull(captured);
        Assert.Equal("authorization.denied", captured.EventName);
        Assert.Equal(AuditEventSources.OpenId, captured.EventSource);
        Assert.Equal(AuditOutcome.Denied, captured.Outcome);
        Assert.Equal(AuditActor.EndUser("subject-1"), captured.Actor);
        Assert.Equal("api/authorize", captured.Request?.Endpoint);
        Assert.Equal("login_required", captured.Error?.Code);
        Assert.Equal(7, captured.EventId.Version);
    }

    #endregion

    #region Client Authentication Tests

    [Fact]
    public async Task RecordClientAuthenticationFailedAsync_PublishesFailedAuditEventWithError()
    {
        var mockContext = MockRepository.Create<OpenIdContext>();
        var mockTenant = MockRepository.Create<OpenIdTenant>();
        var mockPublisher = MockRepository.Create<IEventPublisher>();

        mockContext.Setup(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        mockContext.Setup(x => x.Http).Returns(CreateHttpContext("api/token")).Verifiable();
        mockTenant.Setup(x => x.TenantId).Returns("tenant-1").Verifiable();
        SetupTenantEnvironmentType(mockTenant, "production");

        ClientAuthenticationFailedAuditEvent? captured = null;
        mockPublisher
            .Setup(x =>
                x.PublishAsync(
                    It.IsAny<ClientAuthenticationFailedAuditEvent>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (ClientAuthenticationFailedAuditEvent auditEvent, CancellationToken _) =>
                    captured = auditEvent
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var recorder = new DefaultAuditEventRecorder(mockPublisher.Object, TimeProvider.System);

        await recorder.RecordClientAuthenticationFailedAsync(
            mockContext.Object,
            "client-1",
            OpenIdProtocol.ClientAuthenticationMethods.PrivateKeyJwt,
            "client_not_found",
            CancellationToken.None
        );

        Assert.NotNull(captured);
        Assert.Equal("client.authentication", captured.EventName);
        Assert.Equal(AuditEventSources.OpenId, captured.EventSource);
        Assert.Equal(AuditOutcome.Failure, captured.Outcome);
        Assert.Equal("client-1", captured.ClientId);
        Assert.Equal(AuditActor.Client("client-1"), captured.Actor);
        Assert.Equal("api/token", captured.Request?.Endpoint);
        Assert.Equal(
            OpenIdProtocol.ClientAuthenticationMethods.PrivateKeyJwt,
            captured.AuthenticationMethod
        );
        Assert.Equal("client_not_found", captured.Error?.Code);
        Assert.Null(captured.SubjectId);
        Assert.Equal(7, captured.EventId.Version);
    }

    #endregion

    #region RecordOpenIdErrorAsync Tests

    [Fact]
    public async Task RecordOpenIdErrorAsync_WhenGenericError_PublishesFailureOutcomeWithMetadata()
    {
        var mockContext = MockRepository.Create<OpenIdContext>();
        var mockTenant = MockRepository.Create<OpenIdTenant>();
        var mockError = MockRepository.Create<IOpenIdError>();
        var mockPublisher = MockRepository.Create<IEventPublisher>();

        mockContext.Setup(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        mockContext.Setup(x => x.Http).Returns(CreateHttpContext("api/token")).Verifiable();
        mockTenant.Setup(x => x.TenantId).Returns("tenant-1").Verifiable();
        SetupTenantEnvironmentType(mockTenant, "production");
        mockError.Setup(x => x.Code).Returns("invalid_request").Verifiable();
        mockError
            .Setup(x => x.Description)
            .Returns("The request is missing a parameter.")
            .Verifiable();
        mockError.Setup(x => x.StatusCode).Returns(400).Verifiable();

        OpenIdErrorEvent? captured = null;
        mockPublisher
            .Setup(x => x.PublishAsync(It.IsAny<OpenIdErrorEvent>(), It.IsAny<CancellationToken>()))
            .Callback((OpenIdErrorEvent auditEvent, CancellationToken _) => captured = auditEvent)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var recorder = new DefaultAuditEventRecorder(mockPublisher.Object, TimeProvider.System);

        await recorder.RecordOpenIdErrorAsync(
            mockContext.Object,
            mockError.Object,
            CancellationToken.None
        );

        Assert.NotNull(captured);
        Assert.Equal("openid.error", captured.EventName);
        Assert.Equal(AuditEventSources.OpenId, captured.EventSource);
        Assert.Equal(AuditOutcome.Failure, captured.Outcome);
        Assert.Equal("tenant-1", captured.TenantId);
        Assert.Equal("production", captured.TenantEnvironmentType);
        Assert.Equal(AuditActor.Anonymous, captured.Actor);
        Assert.Equal("api/token", captured.Request?.Endpoint);
        Assert.Equal(
            new AuditError("invalid_request", "The request is missing a parameter.", 400),
            captured.Error
        );
        Assert.Empty(captured.Resources);
        Assert.Equal(7, captured.EventId.Version);
    }

    [Fact]
    public async Task RecordOpenIdErrorAsync_WhenAccessDenied_PublishesDeniedOutcome()
    {
        var mockContext = MockRepository.Create<OpenIdContext>();
        var mockTenant = MockRepository.Create<OpenIdTenant>();
        var mockError = MockRepository.Create<IOpenIdError>();
        var mockPublisher = MockRepository.Create<IEventPublisher>();

        mockContext.Setup(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        mockContext.Setup(x => x.Http).Returns(CreateHttpContext("api/authorize")).Verifiable();
        mockTenant.Setup(x => x.TenantId).Returns("tenant-1").Verifiable();
        SetupTenantEnvironmentType(mockTenant, "production");
        mockError.Setup(x => x.Code).Returns(OpenIdProtocol.ErrorCodes.AccessDenied).Verifiable();
        mockError.Setup(x => x.Description).Returns((string?)null).Verifiable();
        mockError.Setup(x => x.StatusCode).Returns((int?)null).Verifiable();

        OpenIdErrorEvent? captured = null;
        mockPublisher
            .Setup(x => x.PublishAsync(It.IsAny<OpenIdErrorEvent>(), It.IsAny<CancellationToken>()))
            .Callback((OpenIdErrorEvent auditEvent, CancellationToken _) => captured = auditEvent)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var recorder = new DefaultAuditEventRecorder(mockPublisher.Object, TimeProvider.System);

        await recorder.RecordOpenIdErrorAsync(
            mockContext.Object,
            mockError.Object,
            CancellationToken.None
        );

        Assert.NotNull(captured);
        Assert.Equal(AuditOutcome.Denied, captured.Outcome);
        Assert.Equal(AuditActor.Anonymous, captured.Actor);
        Assert.Equal("api/authorize", captured.Request?.Endpoint);
        Assert.Equal(new AuditError(OpenIdProtocol.ErrorCodes.AccessDenied), captured.Error);
    }

    #endregion

    #region RecordRefreshTokenReplayAsync Tests

    [Fact]
    public async Task RecordRefreshTokenReplayAsync_PublishesReplayAuditEventWithDeniedOutcome()
    {
        var mockContext = MockRepository.Create<OpenIdContext>();
        var mockTenant = MockRepository.Create<OpenIdTenant>();
        var mockClient = MockRepository.Create<OpenIdClient>();
        var mockPublisher = MockRepository.Create<IEventPublisher>();

        mockContext.Setup(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        mockContext.Setup(x => x.Http).Returns(CreateHttpContext("api/token")).Verifiable();
        mockTenant.Setup(x => x.TenantId).Returns("tenant-1").Verifiable();
        SetupTenantEnvironmentType(mockTenant, "production");
        mockClient.Setup(x => x.ClientId).Returns("client-1").Verifiable();

        RefreshTokenReplayAuditEvent? captured = null;
        mockPublisher
            .Setup(x =>
                x.PublishAsync(
                    It.IsAny<RefreshTokenReplayAuditEvent>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (RefreshTokenReplayAuditEvent auditEvent, CancellationToken _) =>
                    captured = auditEvent
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var recorder = new DefaultAuditEventRecorder(mockPublisher.Object, TimeProvider.System);

        await recorder.RecordRefreshTokenReplayAsync(
            mockContext.Object,
            mockClient.Object,
            "subject-1",
            CancellationToken.None
        );

        Assert.NotNull(captured);
        Assert.Equal("token.refresh.replay", captured.EventName);
        Assert.Equal(AuditEventSources.OpenId, captured.EventSource);
        Assert.Equal(AuditOutcome.Denied, captured.Outcome);
        Assert.Equal("tenant-1", captured.TenantId);
        Assert.Equal("production", captured.TenantEnvironmentType);
        Assert.Equal("client-1", captured.ClientId);
        Assert.Equal("subject-1", captured.SubjectId);
        Assert.Equal(AuditActor.Client("client-1"), captured.Actor);
        Assert.Equal("api/token", captured.Request?.Endpoint);
        Assert.Equal(7, captured.EventId.Version);
    }

    #endregion

    #region RecordRefreshTokenRetryAsync Tests

    [Fact]
    public async Task RecordRefreshTokenRetryAsync_PublishesRetryAuditEventWithSuccessOutcome()
    {
        var mockContext = MockRepository.Create<OpenIdContext>();
        var mockTenant = MockRepository.Create<OpenIdTenant>();
        var mockClient = MockRepository.Create<OpenIdClient>();
        var mockPublisher = MockRepository.Create<IEventPublisher>();

        mockContext.Setup(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        mockContext.Setup(x => x.Http).Returns(CreateHttpContext("api/token")).Verifiable();
        mockTenant.Setup(x => x.TenantId).Returns("tenant-1").Verifiable();
        SetupTenantEnvironmentType(mockTenant, "production");
        mockClient.Setup(x => x.ClientId).Returns("client-1").Verifiable();

        RefreshTokenRetryAuditEvent? captured = null;
        mockPublisher
            .Setup(x =>
                x.PublishAsync(
                    It.IsAny<RefreshTokenRetryAuditEvent>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (RefreshTokenRetryAuditEvent auditEvent, CancellationToken _) =>
                    captured = auditEvent
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var recorder = new DefaultAuditEventRecorder(mockPublisher.Object, TimeProvider.System);

        await recorder.RecordRefreshTokenRetryAsync(
            mockContext.Object,
            mockClient.Object,
            "subject-1",
            CancellationToken.None
        );

        Assert.NotNull(captured);
        Assert.Equal("token.refresh.retry", captured.EventName);
        Assert.Equal(AuditEventSources.OpenId, captured.EventSource);
        Assert.Equal(AuditOutcome.Success, captured.Outcome);
        Assert.Equal("tenant-1", captured.TenantId);
        Assert.Equal("production", captured.TenantEnvironmentType);
        Assert.Equal("client-1", captured.ClientId);
        Assert.Equal("subject-1", captured.SubjectId);
        Assert.Equal(AuditActor.Client("client-1"), captured.Actor);
        Assert.Equal("api/token", captured.Request?.Endpoint);
        Assert.Equal(7, captured.EventId.Version);
    }

    #endregion

    #region Session Lifecycle Tests

    [Fact]
    public async Task RecordSessionCreatedAsync_PublishesSessionCreatedAuditEvent()
    {
        var mockContext = MockRepository.Create<OpenIdContext>();
        var mockTenant = MockRepository.Create<OpenIdTenant>();
        var mockPublisher = MockRepository.Create<IEventPublisher>();

        mockContext.Setup(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        mockTenant.Setup(x => x.TenantId).Returns("tenant-1").Verifiable();
        SetupTenantEnvironmentType(mockTenant, "production");
        mockContext.Setup(x => x.Http).Returns(CreateHttpContext("login")).Verifiable();

        SessionCreatedAuditEvent? captured = null;
        mockPublisher
            .Setup(x =>
                x.PublishAsync(It.IsAny<SessionCreatedAuditEvent>(), It.IsAny<CancellationToken>())
            )
            .Callback(
                (SessionCreatedAuditEvent auditEvent, CancellationToken _) => captured = auditEvent
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var recorder = new DefaultAuditEventRecorder(mockPublisher.Object, TimeProvider.System);

        await recorder.RecordSessionCreatedAsync(
            mockContext.Object,
            "subject-1",
            "sid-1",
            CancellationToken.None
        );

        Assert.NotNull(captured);
        Assert.Equal("session.created", captured.EventName);
        Assert.Equal(AuditEventSources.OpenId, captured.EventSource);
        Assert.Equal(AuditOutcome.Success, captured.Outcome);
        Assert.Equal("tenant-1", captured.TenantId);
        Assert.Equal("production", captured.TenantEnvironmentType);
        Assert.Equal("subject-1", captured.SubjectId);
        Assert.Equal(AuditActor.EndUser("subject-1"), captured.Actor);
        Assert.Equal("login", captured.Request?.Endpoint);
        Assert.Equal("sid-1", captured.SessionId);
        var resource = Assert.Single(captured.Resources);
        Assert.Equal(new AuditResource(AuditResourceTypes.Session, "sid-1"), resource);
        Assert.Equal(7, captured.EventId.Version);
    }

    [Theory]
    [InlineData(SessionTerminationReasons.GlobalSignOut)]
    [InlineData("password_changed")]
    public async Task RecordSessionTerminatedAsync_PublishesSessionTerminatedAuditEventWithReason(
        string reason
    )
    {
        var mockContext = MockRepository.Create<OpenIdContext>();
        var mockTenant = MockRepository.Create<OpenIdTenant>();
        var mockPublisher = MockRepository.Create<IEventPublisher>();

        mockContext.Setup(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        mockTenant.Setup(x => x.TenantId).Returns("tenant-1").Verifiable();
        SetupTenantEnvironmentType(mockTenant, "production");
        mockContext.Setup(x => x.Http).Returns(CreateHttpContext("api/sessions")).Verifiable();

        SessionTerminatedAuditEvent? captured = null;
        mockPublisher
            .Setup(x =>
                x.PublishAsync(
                    It.IsAny<SessionTerminatedAuditEvent>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (SessionTerminatedAuditEvent auditEvent, CancellationToken _) =>
                    captured = auditEvent
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var recorder = new DefaultAuditEventRecorder(mockPublisher.Object, TimeProvider.System);

        await recorder.RecordSessionTerminatedAsync(
            mockContext.Object,
            reason,
            "subject-1",
            sessionId: null,
            CancellationToken.None
        );

        Assert.NotNull(captured);
        Assert.Equal("session.terminated", captured.EventName);
        Assert.Equal(AuditEventSources.OpenId, captured.EventSource);
        Assert.Equal(AuditOutcome.Success, captured.Outcome);
        Assert.Equal("tenant-1", captured.TenantId);
        Assert.Equal("production", captured.TenantEnvironmentType);
        Assert.Equal("subject-1", captured.SubjectId);
        Assert.Equal(AuditActor.System, captured.Actor);
        Assert.Equal("api/sessions", captured.Request?.Endpoint);
        Assert.Null(captured.SessionId);
        Assert.Empty(captured.Resources);
        Assert.Equal(reason, captured.TerminationReason);
        Assert.Equal(7, captured.EventId.Version);
    }

    #endregion
}
