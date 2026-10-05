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

using Moq;
using NCode.Identity.Events;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Tenants;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Auditing;

public sealed class DefaultAuditEventRecorderTests : IDisposable
{
    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);

    public void Dispose() => MockRepository.Verify();

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
        Assert.Equal("token.issued", captured.Action);
        Assert.Equal(AuditOutcome.Success, captured.Outcome);
        Assert.Equal("access_token", captured.TokenType);
        Assert.Equal("client-1", captured.ClientId);
        Assert.Equal("tenant-1", captured.TenantId);
        Assert.Equal("subject-1", captured.SubjectId);
        Assert.NotEqual(Guid.Empty, captured.EventId);
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
        Assert.Equal("token.revoked", captured.Action);
        Assert.Equal(AuditOutcome.Success, captured.Outcome);
        Assert.Equal("client-1", captured.ClientId);
        Assert.Equal("tenant-1", captured.TenantId);
        Assert.Equal("subject-1", captured.SubjectId);
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
        Assert.Equal("authorization.granted", captured.Action);
        Assert.Equal(AuditOutcome.Success, captured.Outcome);
        Assert.Equal("subject-1", captured.SubjectId);
    }

    [Fact]
    public async Task RecordAuthorizationDeniedAsync_PublishesDeniedAuditEventWithReason()
    {
        var mockContext = MockRepository.Create<OpenIdContext>();
        var mockTenant = MockRepository.Create<OpenIdTenant>();
        var mockClient = MockRepository.Create<OpenIdClient>();
        var mockPublisher = MockRepository.Create<IEventPublisher>();

        mockContext.Setup(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        mockTenant.Setup(x => x.TenantId).Returns("tenant-1").Verifiable();
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
        Assert.Equal("authorization.denied", captured.Action);
        Assert.Equal(AuditOutcome.Denied, captured.Outcome);
        Assert.Equal("login_required", captured.Reason);
    }

    #endregion

    #region Client Authentication Tests

    [Fact]
    public async Task RecordClientAuthenticationFailedAsync_PublishesFailedAuditEventWithReason()
    {
        var mockContext = MockRepository.Create<OpenIdContext>();
        var mockTenant = MockRepository.Create<OpenIdTenant>();
        var mockPublisher = MockRepository.Create<IEventPublisher>();

        mockContext.Setup(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        mockTenant.Setup(x => x.TenantId).Returns("tenant-1").Verifiable();

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
            "client_not_found",
            CancellationToken.None
        );

        Assert.NotNull(captured);
        Assert.Equal("client.authentication", captured.Action);
        Assert.Equal(AuditOutcome.Failure, captured.Outcome);
        Assert.Equal("client-1", captured.ClientId);
        Assert.Equal("client_not_found", captured.Reason);
        Assert.Null(captured.SubjectId);
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
        mockContext.Setup(x => x.EndpointName).Returns("api/token").Verifiable();
        mockTenant.Setup(x => x.TenantId).Returns("tenant-1").Verifiable();
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
        Assert.Equal("openid.error", captured.Action);
        Assert.Equal(AuditOutcome.Failure, captured.Outcome);
        Assert.Equal("tenant-1", captured.TenantId);
        Assert.Equal("invalid_request", captured.ErrorCode);
        Assert.Equal("The request is missing a parameter.", captured.ErrorDescription);
        Assert.Equal(400, captured.StatusCode);
        Assert.Equal("api/token", captured.EndpointName);
    }

    [Fact]
    public async Task RecordOpenIdErrorAsync_WhenAccessDenied_PublishesDeniedOutcome()
    {
        var mockContext = MockRepository.Create<OpenIdContext>();
        var mockTenant = MockRepository.Create<OpenIdTenant>();
        var mockError = MockRepository.Create<IOpenIdError>();
        var mockPublisher = MockRepository.Create<IEventPublisher>();

        mockContext.Setup(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        mockContext.Setup(x => x.EndpointName).Returns("api/authorize").Verifiable();
        mockTenant.Setup(x => x.TenantId).Returns("tenant-1").Verifiable();
        mockError.Setup(x => x.Code).Returns(OpenIdConstants.ErrorCodes.AccessDenied).Verifiable();
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
        Assert.Equal(OpenIdConstants.ErrorCodes.AccessDenied, captured.ErrorCode);
        Assert.Null(captured.ErrorDescription);
        Assert.Null(captured.StatusCode);
        Assert.Equal("api/authorize", captured.EndpointName);
    }

    #endregion
}
