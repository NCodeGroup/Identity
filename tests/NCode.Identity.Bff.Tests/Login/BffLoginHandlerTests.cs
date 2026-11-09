// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Bff.Auditing;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Login;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.Bff.Tests.Diagnostics;
using NCode.Identity.Bff.Tests.Frontends;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Client;
using NCode.Identity.SharedState;
using Xunit;

namespace NCode.Identity.Bff.Tests.Login;

public class BffLoginHandlerTests : BaseTests
{
    private static DateTimeOffset Now { get; } = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    private Mock<IAuthorizationRequestClient> Client { get; }
    private Mock<IBffAuditRecorder> AuditRecorder { get; }
    private Mock<IBffSessionStore> Store { get; }

    public BffLoginHandlerTests()
    {
        Client = CreateStrictMock<IAuthorizationRequestClient>();
        AuditRecorder = CreateStrictMock<IBffAuditRecorder>();
        Store = CreateStrictMock<IBffSessionStore>();
    }

    private BffLoginHandler CreateHandler(
        Action<BffFrontendOptions>? configure = null,
        BffLoginTransactionService? transactionService = null
    )
    {
        var timeProvider = new FakeTimeProvider(Now);
        return new BffLoginHandler(
            BffTestFrontends.CreateCatalog(x =>
            {
                ConfigureDefaults(x);
                configure?.Invoke(x);
            }),
            Client.Object,
            transactionService
                ?? new BffLoginTransactionService(BffTestTelemetry.Instrument(Store.Object)),
            new BffCookies(timeProvider),
            AuditRecorder.Object,
            timeProvider
        );
    }

    private static void ConfigureDefaults(BffFrontendOptions options)
    {
        options.Scopes = ["profile"];
        options.ReturnUrls = ["/app"];
        options.LoginParameters = [OpenIdProtocol.Parameters.Prompt];
    }

    private static DefaultHttpContext CreateHttpContext(string query)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("attacker.example");
        httpContext.Request.QueryString = new QueryString(query);
        httpContext.Response.Body = new MemoryStream();
        return httpContext;
    }

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_ReturnUrlNotAllowed_AnswersBadRequestWithoutCallingClient()
    {
        var httpContext = CreateHttpContext("?returnUrl=/other");

        await CreateHandler().HandleAsync(httpContext);

        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
        Assert.Equal(
            "no-store, no-cache, max-age=0",
            httpContext.Response.Headers.CacheControl.ToString()
        );
    }

    [Fact]
    public async Task HandleAsync_DuplicateParameter_AnswersBadRequest()
    {
        var httpContext = CreateHttpContext("?returnUrl=/app&prompt=login&prompt=consent");

        await CreateHandler().HandleAsync(httpContext);

        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_InvalidMaxAge_AnswersBadRequest()
    {
        var httpContext = CreateHttpContext("?returnUrl=/app&max_age=-5");

        await CreateHandler(x => x.LoginParameters = [OpenIdProtocol.Parameters.MaxAge])
            .HandleAsync(httpContext);

        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_ClientReturnsError_RedirectsToErrorUrl()
    {
        var httpContext = CreateHttpContext("?returnUrl=/app&prompt=login&ignored=1");
        AuthorizationRequest? captured = null;
        Client
            .Setup(x =>
                x.CreateAuthorizationUriAsync(
                    It.IsAny<ClientRegistration>(),
                    It.IsAny<AuthorizationRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<ClientRegistration, AuthorizationRequest, CancellationToken>(
                (_, request, _) => captured = request
            )
            .Returns(
                new ValueTask<AuthorizationRequestResult>(
                    new AuthorizationRequestResult(
                        new ProtocolError(OpenIdProtocol.ErrorCodes.InvalidRequest, 400)
                    )
                )
            )
            .Verifiable();
        AuditRecorder
            .Setup(x =>
                x.LoginFailedAsync(
                    httpContext,
                    BffLoginFailureReasons.AuthorizationRequestFailed,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await CreateHandler().HandleAsync(httpContext);

        Assert.Equal(StatusCodes.Status302Found, httpContext.Response.StatusCode);
        Assert.Equal(
            $"/?error={BffLoginFailureReasons.AuthorizationRequestFailed}",
            httpContext.Response.Headers.Location.ToString()
        );
        Assert.NotNull(captured);
        Assert.Equal("openid profile", captured.Scope);
        Assert.False(string.IsNullOrEmpty(captured.Nonce));
        Assert.Equal(OpenIdProtocol.ResponseModes.Query, captured.ResponseMode);
        Assert.Equal("https://app.example.com/bff/callback", captured.RedirectUri);
        var additional = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(
            captured.AdditionalParameters
        );
        var prompt = Assert.Single(additional);
        Assert.Equal(OpenIdProtocol.Parameters.Prompt, prompt.Key);
        Assert.Equal("login", prompt.Value);
    }

    [Theory]
    [InlineData(new[] { "profile" }, "openid profile")]
    [InlineData(
        new[] { "openid", "profile", "offline_access", "profile" },
        "openid profile offline_access"
    )]
    public async Task HandleAsync_ConfiguredScopes_RequestsOpenIdAndEachConfiguredScopeOnce(
        string[] configuredScopes,
        string expectedScope
    )
    {
        var httpContext = CreateHttpContext("?returnUrl=/app");
        AuthorizationRequest? captured = null;
        Client
            .Setup(x =>
                x.CreateAuthorizationUriAsync(
                    It.IsAny<ClientRegistration>(),
                    It.IsAny<AuthorizationRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<ClientRegistration, AuthorizationRequest, CancellationToken>(
                (_, request, _) => captured = request
            )
            .Returns(
                new ValueTask<AuthorizationRequestResult>(
                    new AuthorizationRequestResult(
                        new ProtocolError(OpenIdProtocol.ErrorCodes.InvalidRequest, 400)
                    )
                )
            )
            .Verifiable();
        AuditRecorder
            .Setup(x =>
                x.LoginFailedAsync(
                    httpContext,
                    BffLoginFailureReasons.AuthorizationRequestFailed,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await CreateHandler(x => x.Scopes = [.. configuredScopes]).HandleAsync(httpContext);

        Assert.NotNull(captured);
        Assert.Equal(expectedScope, captured.Scope);
    }

    [Fact]
    public async Task HandleAsync_ProviderUnavailable_RedirectsToErrorUrl()
    {
        var httpContext = CreateHttpContext("?returnUrl=/app");
        Client
            .Setup(x =>
                x.CreateAuthorizationUriAsync(
                    It.IsAny<ClientRegistration>(),
                    It.IsAny<AuthorizationRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new OpenIdProviderException("metadata unavailable"))
            .Verifiable();
        AuditRecorder
            .Setup(x =>
                x.LoginFailedAsync(
                    httpContext,
                    BffLoginFailureReasons.ProviderUnavailable,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await CreateHandler().HandleAsync(httpContext);

        Assert.Equal(StatusCodes.Status302Found, httpContext.Response.StatusCode);
        Assert.Equal(
            $"/?error={BffLoginFailureReasons.ProviderUnavailable}",
            httpContext.Response.Headers.Location.ToString()
        );
    }

    [Fact]
    public async Task HandleAsync_Success_StoresTransactionSetsCookieAndRedirects()
    {
        var serviceCollection = new ServiceCollection();
        // The in-process value set expires entries by its TimeProvider, so it shares the handler's fixed clock.
        serviceCollection.AddSingleton<TimeProvider>(new FakeTimeProvider(Now));
        serviceCollection
            .AddSharedStateServices()
            .AddInProcessExpiringValueSet()
            .AddInProcessExpiringIndexSet();
        await using var provider = serviceCollection.BuildServiceProvider();
        var values = provider.GetRequiredService<IExpiringValueSet>();
        var indexes = provider.GetRequiredService<IExpiringIndexSet>();
        var transactionService = new BffLoginTransactionService(
            BffTestTelemetry.Instrument(new DefaultBffSessionStore(values, indexes))
        );

        var httpContext = CreateHttpContext("?returnUrl=/app");
        var authorizationUri = new Uri("https://op.example.com/authorize?client_id=bff");
        AuthorizationRequest? captured = null;
        Client
            .Setup(x =>
                x.CreateAuthorizationUriAsync(
                    It.IsAny<ClientRegistration>(),
                    It.IsAny<AuthorizationRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<ClientRegistration, AuthorizationRequest, CancellationToken>(
                (_, request, _) => captured = request
            )
            .Returns(
                new ValueTask<AuthorizationRequestResult>(
                    new AuthorizationRequestResult(authorizationUri) { DPoPJwkThumbprint = "jkt" }
                )
            )
            .Verifiable();

        await CreateHandler(transactionService: transactionService).HandleAsync(httpContext);

        Assert.Equal(StatusCodes.Status302Found, httpContext.Response.StatusCode);
        Assert.Equal(
            authorizationUri.AbsoluteUri,
            httpContext.Response.Headers.Location.ToString()
        );
        var setCookie = httpContext.Response.Headers.SetCookie.ToString();
        var cookiePrefix = BffTestFrontends.LoginCookieName + "=";
        Assert.StartsWith(cookiePrefix, setCookie);
        var initiator = setCookie[cookiePrefix.Length..].Split(';')[0];

        Assert.NotNull(captured);
        var result = await transactionService.TakeAsync(
            captured.State,
            initiator,
            CancellationToken.None
        );

        Assert.Null(result.FailureReason);
        var transaction = Assert.IsType<BffLoginTransaction>(result.Transaction);
        Assert.Equal("/app", transaction.ReturnUrl);
        Assert.Equal(BffTestFrontends.Name, transaction.Frontend);
        Assert.Equal("jkt", transaction.DPoPJwkThumbprint);
        Assert.Equal(captured.Nonce, transaction.Nonce);
    }

    #endregion
}
