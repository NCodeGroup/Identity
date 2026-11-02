// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Moq;
using NCode.Buffers;
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Identity.OpenId.Authentication.Sessions;
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
    private static readonly string[] RequiredAudiences = ["urn:test:resource"];

    private Mock<IAuditEventRecorder> MockAuditEventRecorder { get; set; } = null!;
    private Mock<ISessionService> MockSessionService { get; set; } = null!;
    private Mock<OpenIdContext> MockContext { get; set; } = null!;

    #region Scaffolding

    // A request that ends before it reads the subject.
    private DefaultAuthenticateSubjectHandler CreateHandler()
    {
        MockAuditEventRecorder = CreateLooseMock<IAuditEventRecorder>();
        MockSessionService = CreateStrictMock<ISessionService>();

        return new DefaultAuthenticateSubjectHandler(
            CreateStrictMock<IStoreManagerFactory>().Object,
            CreateStrictMock<IPrincipalResolver>().Object,
            MockAuditEventRecorder.Object,
            MockSessionService.Object
        );
    }

    // A request that reads the subject; a subject with an id is normalized and resolved to its principal.
    private DefaultAuthenticateSubjectHandler CreateHandler(
        string? subjectId,
        string? resolvedPrincipalId = null
    )
    {
        var mockFactory = CreateStrictMock<IStoreManagerFactory>();
        var mockResolver = CreateStrictMock<IPrincipalResolver>();

        MockContext
            .Setup(x => x.GetSubjectId(It.IsAny<ClaimsPrincipal>()))
            .Returns(subjectId)
            .Verifiable();
        if (subjectId is not null)
        {
            SetupPrincipalResolution(mockFactory, mockResolver, subjectId, resolvedPrincipalId);
        }

        MockAuditEventRecorder = CreateLooseMock<IAuditEventRecorder>();
        MockSessionService = CreateStrictMock<ISessionService>();

        return new DefaultAuthenticateSubjectHandler(
            mockFactory.Object,
            mockResolver.Object,
            MockAuditEventRecorder.Object,
            MockSessionService.Object
        );
    }

    private void SetupPrincipalResolution(
        Mock<IStoreManagerFactory> mockFactory,
        Mock<IPrincipalResolver> mockResolver,
        string subjectId,
        string? resolvedPrincipalId
    )
    {
        var mockManager = CreateStrictMock<IStoreManager>();

        mockFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockManager.Object)
            .Verifiable();
        mockManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        mockManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
        // The resolver maps the authenticated subject to the stable principal id carried into the ticket (ADR-1035).
        mockResolver
            .Setup(x =>
                x.ResolvePrincipalIdAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<IStoreManager>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(resolvedPrincipalId ?? subjectId)
            .Verifiable();
        MockContext
            .Setup(x => x.NormalizeSubjectId(It.IsAny<ClaimsPrincipal>(), It.IsAny<string>()))
            .Returns((ClaimsPrincipal subject, string _) => subject)
            .Verifiable();
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

    // An access token that authenticates a principal completes the request, which reads the error factory.
    private void SetupCompletionErrorFactory() =>
        MockContext
            .SetupGet(x => x.ErrorFactory)
            .Returns(CreateLooseMock<IOpenIdErrorFactory>().Object)
            .Verifiable();

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

    private static AuthenticateResult SuccessResultWithSession(string sessionId)
    {
        var identity = new ClaimsIdentity(
            [new Claim(JoseClaimNames.Payload.Sid, sessionId)],
            "scheme"
        );
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

        var result = await CreateHandler().HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsUndefined);
        // No credentials were presented; this is not a login event and must not be audited.
        MockAuditEventRecorder.Verify(
            x =>
                x.RecordSubjectAuthenticationFailedAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<string>(),
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
        SetupAuthenticate(
            mockAuthService,
            AuthenticateResult.Fail(new InvalidOperationException("boom"))
        );

        var result = await CreateHandler().HandleAsync(command, CancellationToken.None);

        Assert.True(result.HasError);
        Assert.Same(mockError.Object, result.Error);
        MockAuditEventRecorder.Verify(
            x =>
                x.RecordSubjectAuthenticationFailedAsync(
                    command.OpenIdContext,
                    "authentication_failed",
                    "Failed to authenticate the end-user.",
                    null,
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
                    "subject_id_missing",
                    "Unable to determine the end-user's subject id.",
                    null,
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

        // The resolver maps the raw subject to a stable principal id; the ticket carries the resolved id (ADR-1035).
        var result = await CreateHandler("subject-id", resolvedPrincipalId: "principal-99")
            .HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsAuthenticated);
        Assert.Equal("principal-99", result.Ticket.Value.PrincipalId);
    }

    [Fact]
    public async Task HandleAsync_WhenTheCookieSessionHasEnded_ReturnsUndefined()
    {
        var (command, mockAuthService, _) = CreateScaffold();
        SetupAuthenticate(mockAuthService, SuccessResultWithSession("sid-1"));

        var handler = CreateHandler();
        MockSessionService
            .Setup(x =>
                x.ValidateAndTouchAsync(
                    command.OpenIdContext,
                    "sid-1",
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<bool>(false))
            .Verifiable();

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsUndefined);
    }

    [Fact]
    public async Task HandleAsync_WhenTheCookieSessionIsActive_ReturnsAuthenticated()
    {
        var (command, mockAuthService, _) = CreateScaffold();
        SetupAuthenticate(mockAuthService, SuccessResultWithSession("sid-1"));

        var handler = CreateHandler("subject-id");
        MockSessionService
            .Setup(x =>
                x.ValidateAndTouchAsync(
                    command.OpenIdContext,
                    "sid-1",
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<bool>(true))
            .Verifiable();

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsAuthenticated);
        Assert.Equal("subject-id", result.Ticket.Value.PrincipalId);
    }

    #endregion

    #region Bearer Access Token Tests

    private (
        AuthenticateResourceRequestCommand command,
        Mock<IMediator> mediator
    ) CreateBearerScaffold(AuthenticateAccessTokenDisposition disposition)
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        MockContext = mockContext;
        var mockMediator = CreateStrictMock<IMediator>();

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization = "Bearer the-access-token";

        mockContext.SetupGet(x => x.Http).Returns(httpContext).Verifiable();
        mockContext.SetupGet(x => x.Mediator).Returns(mockMediator.Object).Verifiable();

        // The bearer token is validated via the in-environment self-issued access-token command, not AuthenticateAsync.
        mockMediator
            .Setup(x =>
                x.SendAsync<AuthenticateAccessTokenCommand, AuthenticateAccessTokenDisposition>(
                    It.Is<AuthenticateAccessTokenCommand>(c =>
                        ReferenceEquals(c.RequiredAudiences, RequiredAudiences)
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<AuthenticateAccessTokenDisposition>(disposition))
            .Verifiable();

        var command = new AuthenticateResourceRequestCommand(mockContext.Object, RequiredAudiences);
        return (command, mockMediator);
    }

    [Fact]
    public async Task HandleAsync_WhenBearerTokenValid_ReturnsTicket()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"));
        var (command, mockMediator) = CreateBearerScaffold(
            new AuthenticateAccessTokenDisposition(principal)
        );

        SetupCompletionErrorFactory();

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
    public async Task HandleAsync_WhenBearerTokenInvalid_ReturnsFailedAndAudits()
    {
        var mockError = CreateLooseMock<IOpenIdError>();
        mockError.SetupGet(x => x.Code).Returns("invalid_token");
        var (command, _) = CreateBearerScaffold(
            new AuthenticateAccessTokenDisposition(mockError.Object)
        );

        var result = await CreateHandler().HandleAsync(command, CancellationToken.None);

        Assert.True(result.HasError);
        Assert.Same(mockError.Object, result.Error);
        MockAuditEventRecorder.Verify(
            x =>
                x.RecordSubjectAuthenticationFailedAsync(
                    command.OpenIdContext,
                    "invalid_token",
                    "Failed to authenticate the end-user.",
                    null,
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

        var result = await CreateHandler().HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsUndefined);
    }

    [Fact]
    public async Task HandleAsync_WhenResourceRequestWithoutAccessToken_ReturnsUndefinedWithoutSession()
    {
        // A protected resource is never authenticated by an interactive session (OpenID Connect Core 1.0 §5.3.1).
        var mockContext = CreateStrictMock<OpenIdContext>();
        MockContext = mockContext;
        mockContext.SetupGet(x => x.Http).Returns(new DefaultHttpContext()).Verifiable();
        var command = new AuthenticateResourceRequestCommand(mockContext.Object, RequiredAudiences);

        var result = await CreateHandler().HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsUndefined);
    }

    [Fact]
    public async Task HandleAsync_WhenSessionRequestPresentsAccessToken_AuthenticatesTheSessionOnly()
    {
        // An interactive endpoint authenticates its session; a presented access token is not a credential there.
        var (command, mockAuthService, _) = CreateScaffold();
        command.OpenIdContext.Http.Request.Headers.Authorization = "Bearer some-access-token";
        SetupAuthenticate(mockAuthService, AuthenticateResult.NoResult());

        var result = await CreateHandler().HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsUndefined);
    }

    #endregion
    #region Form-Body Access Token Tests

    private (
        AuthenticateResourceRequestCommand command,
        Mock<IMediator> mediator
    ) CreateFormBodyScaffold(
        AuthenticateAccessTokenDisposition disposition,
        string expectedAccessToken,
        bool includeAuthorizationHeader = false
    )
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        MockContext = mockContext;
        var mockMediator = CreateStrictMock<IMediator>();

        // RFC 6750 §2.2: a form-encoded POST body carrying 'access_token'.
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.ContentType = "application/x-www-form-urlencoded";
        httpContext.Request.Form = new FormCollection(
            new Dictionary<string, StringValues> { ["access_token"] = "the-body-access-token" }
        );
        if (includeAuthorizationHeader)
        {
            httpContext.Request.Headers.Authorization = "Bearer the-header-access-token";
        }

        mockContext.SetupGet(x => x.Http).Returns(httpContext).Verifiable();
        mockContext.SetupGet(x => x.Mediator).Returns(mockMediator.Object).Verifiable();

        // Strict: the command must carry the token from the expected transport, or the setup never matches.
        mockMediator
            .Setup(x =>
                x.SendAsync<AuthenticateAccessTokenCommand, AuthenticateAccessTokenDisposition>(
                    It.Is<AuthenticateAccessTokenCommand>(c =>
                        c.AccessToken == expectedAccessToken
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<AuthenticateAccessTokenDisposition>(disposition))
            .Verifiable();

        var command = new AuthenticateResourceRequestCommand(mockContext.Object, RequiredAudiences);
        return (command, mockMediator);
    }

    [Fact]
    public async Task HandleAsync_WhenAccessTokenInFormBody_ReturnsTicket()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"));
        var (command, mockMediator) = CreateFormBodyScaffold(
            new AuthenticateAccessTokenDisposition(principal),
            expectedAccessToken: "the-body-access-token"
        );

        SetupCompletionErrorFactory();

        var result = await CreateHandler("subject-id").HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsAuthenticated);
        Assert.Equal("subject-id", result.Ticket.Value.PrincipalId);
    }

    [Fact]
    public async Task HandleAsync_WhenHeaderAndFormBothPresent_PrefersAuthorizationHeader()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"));
        // RFC 6750 §2.1 is primary: the header token must win over a form-body token.
        var (command, mockMediator) = CreateFormBodyScaffold(
            new AuthenticateAccessTokenDisposition(principal),
            expectedAccessToken: "the-header-access-token",
            includeAuthorizationHeader: true
        );

        SetupCompletionErrorFactory();

        var result = await CreateHandler("subject-id").HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsAuthenticated);
    }

    private AuthenticateResourceRequestCommand CreateFormBodyCommand(
        string body,
        IOpenIdErrorFactory? errorFactory = null
    )
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        MockContext = mockContext;

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.ContentType = "application/x-www-form-urlencoded";
        httpContext.Request.Body = new MemoryStream(SecureEncoding.UTF8.GetBytes(body));

        mockContext.SetupGet(x => x.Http).Returns(httpContext).Verifiable();
        // Only an unreadable form reads the error factory.
        if (errorFactory is not null)
        {
            mockContext.SetupGet(x => x.ErrorFactory).Returns(errorFactory).Verifiable();
        }

        return new AuthenticateResourceRequestCommand(mockContext.Object, RequiredAudiences);
    }

    [Fact]
    public async Task HandleAsync_WhenFormBodyUnreadable_ReturnsInvalidRequest()
    {
        var mockError = CreateLooseMock<IOpenIdError>();
        var mockErrorFactory = CreateStrictMock<IOpenIdErrorFactory>();
        mockErrorFactory
            .Setup(x => x.Create(OpenIdProtocol.ErrorCodes.InvalidRequest))
            .Returns(mockError.Object)
            .Verifiable();
        // The form key limit is 2048 characters.
        var command = CreateFormBodyCommand(new string('k', 2049) + "=v", mockErrorFactory.Object);

        var result = await CreateHandler().HandleAsync(command, CancellationToken.None);

        Assert.True(result.HasError);
        Assert.Same(mockError.Object, result.Error);
        mockError.VerifySet(x => x.StatusCode = StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task HandleAsync_WhenFormBodyHasNoAccessToken_ReturnsUndefined()
    {
        var command = CreateFormBodyCommand("a=1");

        var result = await CreateHandler().HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsUndefined);
    }

    #endregion
}
