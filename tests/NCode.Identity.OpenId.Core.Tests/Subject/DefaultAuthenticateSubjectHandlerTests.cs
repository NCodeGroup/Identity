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

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Authentication.Tokens.Commands;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.PrincipalResolution;
using NCode.Mediator;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Subject;

public class DefaultAuthenticateSubjectHandlerTests : BaseTests
{
    private Mock<IAuditEventRecorder> MockAuditEventRecorder { get; set; } = null!;
    private Mock<OpenIdContext> MockContext { get; set; } = null!;

    #region Scaffolding

    private DefaultAuthenticateSubjectHandler CreateHandler(
        string? subjectId,
        string? resolvedPrincipalId = null
    )
    {
        var mockFactory = CreateStrictMock<IStoreManagerFactory>();
        var mockManager = CreateStrictMock<IStoreManager>();
        var mockResolver = CreateStrictMock<IPrincipalResolver>();

        mockFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockManager.Object);
        mockManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        mockManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);
        // The resolver maps the authenticated subject to the stable principal id carried into the ticket (ADR-0035).
        mockResolver
            .Setup(x =>
                x.ResolvePrincipalIdAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<IStoreManager>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(resolvedPrincipalId ?? subjectId ?? string.Empty);

        MockAuditEventRecorder = CreateLooseMock<IAuditEventRecorder>();

        MockContext.Setup(x => x.GetSubjectId(It.IsAny<ClaimsPrincipal>())).Returns(subjectId);
        MockContext
            .Setup(x => x.NormalizeSubjectId(It.IsAny<ClaimsPrincipal>(), It.IsAny<string>()))
            .Returns((ClaimsPrincipal subject, string _) => subject);

        return new DefaultAuthenticateSubjectHandler(
            mockFactory.Object,
            mockResolver.Object,
            MockAuditEventRecorder.Object
        );
    }

    private (
        AuthenticateSubjectCommand command,
        Mock<IAuthenticationService> authService,
        Mock<IOpenIdError> error
    ) CreateScaffold()
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        MockContext = mockContext;
        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        var mockError = CreateLooseMock<IOpenIdError>();
        var mockAuthService = CreateStrictMock<IAuthenticationService>();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(mockAuthService.Object);
        var httpContext = new DefaultHttpContext
        {
            RequestServices = serviceCollection.BuildServiceProvider(),
        };

        mockContext.SetupGet(x => x.Http).Returns(httpContext).Verifiable();
        mockContext.SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object).Verifiable();
        mockErrorFactory.Setup(x => x.Create(It.IsAny<string>())).Returns(mockError.Object);

        var command = new AuthenticateSubjectCommand(mockContext.Object);

        return (command, mockAuthService, mockError);
    }

    private static void SetupAuthenticate(
        Mock<IAuthenticationService> mockAuthService,
        AuthenticateResult result
    ) =>
        mockAuthService
            .Setup(x => x.AuthenticateAsync(It.IsAny<HttpContext>(), It.IsAny<string?>()))
            .ReturnsAsync(result)
            .Verifiable();

    private static AuthenticateResult SuccessResult()
    {
        var identity = new ClaimsIdentity("scheme");
        var principal = new ClaimsPrincipal(identity);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, "scheme"));
    }

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenAuthenticationNone_ReturnsUndefined()
    {
        var (command, mockAuthService, _) = CreateScaffold();
        SetupAuthenticate(mockAuthService, AuthenticateResult.NoResult());

        var result = await CreateHandler("subject-id").HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsUndefined);
        // No credentials were presented; this is not a login event and must not be audited.
        MockAuditEventRecorder.Verify(
            x =>
                x.RecordSubjectAuthenticationFailedAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
        MockAuditEventRecorder.Verify(
            x =>
                x.RecordSubjectAuthenticatedAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task HandleAsync_WhenAuthenticationFailed_ReturnsFailedError()
    {
        var (command, mockAuthService, mockError) = CreateScaffold();
        SetupAuthenticate(mockAuthService, AuthenticateResult.Fail(new Exception("boom")));

        var result = await CreateHandler("subject-id").HandleAsync(command, CancellationToken.None);

        Assert.True(result.HasError);
        Assert.Same(mockError.Object, result.Error);
        MockAuditEventRecorder.Verify(
            x =>
                x.RecordSubjectAuthenticationFailedAsync(
                    command.OpenIdContext,
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task HandleAsync_WhenSubjectIdMissing_ReturnsFailedError()
    {
        var (command, mockAuthService, mockError) = CreateScaffold();
        SetupAuthenticate(mockAuthService, SuccessResult());

        var result = await CreateHandler(subjectId: null)
            .HandleAsync(command, CancellationToken.None);

        Assert.True(result.HasError);
        Assert.Same(mockError.Object, result.Error);
        MockAuditEventRecorder.Verify(
            x =>
                x.RecordSubjectAuthenticationFailedAsync(
                    command.OpenIdContext,
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task HandleAsync_WhenAuthenticated_ReturnsTicket()
    {
        var (command, mockAuthService, _) = CreateScaffold();
        SetupAuthenticate(mockAuthService, SuccessResult());

        var result = await CreateHandler("subject-id").HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsAuthenticated);
        Assert.Equal("subject-id", result.Ticket.Value.PrincipalId);
        MockAuditEventRecorder.Verify(
            x =>
                x.RecordSubjectAuthenticatedAsync(
                    command.OpenIdContext,
                    "subject-id",
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task HandleAsync_WhenAuthenticated_CarriesResolvedPrincipalId()
    {
        var (command, mockAuthService, _) = CreateScaffold();
        SetupAuthenticate(mockAuthService, SuccessResult());

        // The resolver maps the raw subject to a stable principal id; the ticket carries the resolved id (ADR-0035).
        var result = await CreateHandler("subject-id", resolvedPrincipalId: "principal-99")
            .HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsAuthenticated);
        Assert.Equal("principal-99", result.Ticket.Value.PrincipalId);
    }

    #endregion

    #region Bearer Access Token Tests

    private (AuthenticateSubjectCommand command, Mock<IMediator> mediator) CreateBearerScaffold(
        AuthenticateAccessTokenDisposition disposition
    )
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        MockContext = mockContext;
        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        var mockMediator = CreateStrictMock<IMediator>();

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization = "Bearer the-access-token";

        mockContext.SetupGet(x => x.Http).Returns(httpContext).Verifiable();
        mockContext.SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object);
        mockContext.SetupGet(x => x.Mediator).Returns(mockMediator.Object).Verifiable();

        mockMediator
            .Setup(x =>
                x.SendAsync<AuthenticateAccessTokenCommand, AuthenticateAccessTokenDisposition>(
                    It.IsAny<AuthenticateAccessTokenCommand>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<AuthenticateAccessTokenDisposition>(disposition))
            .Verifiable();

        var command = new AuthenticateSubjectCommand(mockContext.Object);
        return (command, mockMediator);
    }

    [Fact]
    public async Task HandleAsync_WhenBearerTokenValid_ReturnsTicket()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"));
        var (command, mockMediator) = CreateBearerScaffold(
            new AuthenticateAccessTokenDisposition(principal)
        );

        var result = await CreateHandler("subject-id").HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsAuthenticated);
        Assert.Equal("subject-id", result.Ticket.Value.PrincipalId);
        // The bearer token is validated via the in-environment self-issued access-token command, not AuthenticateAsync.
        mockMediator.Verify();
        MockAuditEventRecorder.Verify(
            x =>
                x.RecordSubjectAuthenticatedAsync(
                    command.OpenIdContext,
                    "subject-id",
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task HandleAsync_WhenBearerTokenInvalid_ReturnsFailedAndAudits()
    {
        var mockError = CreateLooseMock<IOpenIdError>();
        var (command, _) = CreateBearerScaffold(
            new AuthenticateAccessTokenDisposition(mockError.Object)
        );

        var result = await CreateHandler("subject-id").HandleAsync(command, CancellationToken.None);

        Assert.True(result.HasError);
        Assert.Same(mockError.Object, result.Error);
        MockAuditEventRecorder.Verify(
            x =>
                x.RecordSubjectAuthenticationFailedAsync(
                    command.OpenIdContext,
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task HandleAsync_WhenBearerTokenUndefined_ReturnsUndefined()
    {
        // An empty disposition (no principal, no error) means the token could not be read; surface it as undefined.
        var (command, _) = CreateBearerScaffold(default);

        var result = await CreateHandler("subject-id").HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsUndefined);
    }

    #endregion
}
