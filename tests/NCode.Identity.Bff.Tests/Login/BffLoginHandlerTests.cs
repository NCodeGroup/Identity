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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using NCode.Identity.Bff.Auditing;
using NCode.Identity.Bff.Client;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Login;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Client;
using NCode.Identity.SharedState;
using Xunit;

namespace NCode.Identity.Bff.Tests.Login;

public class BffLoginHandlerTests : BaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IAuthorizationRequestClient> _client;
    private readonly Mock<IBffAuditRecorder> _auditRecorder;
    private readonly Mock<IBffSessionStore> _store;

    public BffLoginHandlerTests()
    {
        _client = CreateStrictMock<IAuthorizationRequestClient>();
        _auditRecorder = CreateStrictMock<IBffAuditRecorder>();
        _store = CreateStrictMock<IBffSessionStore>();
    }

    private BffLoginHandler CreateHandler(
        BffOptions bffOptions,
        BffLoginTransactionService? transactionService = null
    )
    {
        var options = Microsoft.Extensions.Options.Options.Create(bffOptions);
        var timeProvider = new MutableTimeProvider(Now);
        return new BffLoginHandler(
            options,
            new BffClientRegistrationProvider(options),
            _client.Object,
            transactionService ?? new BffLoginTransactionService(_store.Object),
            new BffCookies(options, timeProvider),
            _auditRecorder.Object,
            CreateLooseMock<IHostEnvironment>().Object,
            timeProvider
        );
    }

    private static BffOptions CreateOptions() =>
        new()
        {
            Authority = "https://op.example.com",
            ClientId = "bff",
            Scopes = ["profile"],
            ReturnUrls = ["/app"],
            LoginParameters = [OpenIdProtocol.Parameters.Prompt],
        };

    private static DefaultHttpContext CreateHttpContext(string query)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("app.example.com");
        httpContext.Request.QueryString = new QueryString(query);
        httpContext.Response.Body = new MemoryStream();
        return httpContext;
    }

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_ReturnUrlNotAllowed_AnswersBadRequestWithoutCallingClient()
    {
        var httpContext = CreateHttpContext("?returnUrl=/other");

        await CreateHandler(CreateOptions()).HandleAsync(httpContext);

        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
        Assert.Equal("no-store", httpContext.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task HandleAsync_DuplicateParameter_AnswersBadRequest()
    {
        var httpContext = CreateHttpContext("?returnUrl=/app&prompt=login&prompt=consent");

        await CreateHandler(CreateOptions()).HandleAsync(httpContext);

        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_InvalidMaxAge_AnswersBadRequest()
    {
        var bffOptions = CreateOptions();
        bffOptions.LoginParameters = [OpenIdProtocol.Parameters.MaxAge];
        var httpContext = CreateHttpContext("?returnUrl=/app&max_age=-5");

        await CreateHandler(bffOptions).HandleAsync(httpContext);

        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_ClientReturnsError_RedirectsToErrorUrl()
    {
        var httpContext = CreateHttpContext("?returnUrl=/app&prompt=login&ignored=1");
        AuthorizationRequest? captured = null;
        _client
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
        _auditRecorder
            .Setup(x =>
                x.LoginFailedAsync(
                    httpContext,
                    BffLoginFailureReasons.AuthorizationRequestFailed,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await CreateHandler(CreateOptions()).HandleAsync(httpContext);

        Assert.Equal(StatusCodes.Status302Found, httpContext.Response.StatusCode);
        Assert.Equal(
            $"/?error={BffLoginFailureReasons.AuthorizationRequestFailed}",
            httpContext.Response.Headers.Location.ToString()
        );
        Assert.NotNull(captured);
        Assert.Equal("openid profile offline_access", captured.Scope);
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

    [Fact]
    public async Task HandleAsync_ProviderUnavailable_RedirectsToErrorUrl()
    {
        var httpContext = CreateHttpContext("?returnUrl=/app");
        _client
            .Setup(x =>
                x.CreateAuthorizationUriAsync(
                    It.IsAny<ClientRegistration>(),
                    It.IsAny<AuthorizationRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new OpenIdProviderException("metadata unavailable"))
            .Verifiable();
        _auditRecorder
            .Setup(x =>
                x.LoginFailedAsync(
                    httpContext,
                    BffLoginFailureReasons.ProviderUnavailable,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await CreateHandler(CreateOptions()).HandleAsync(httpContext);

        Assert.Equal(StatusCodes.Status302Found, httpContext.Response.StatusCode);
        Assert.Equal(
            $"/?error={BffLoginFailureReasons.ProviderUnavailable}",
            httpContext.Response.Headers.Location.ToString()
        );
    }

    [Fact]
    public async Task HandleAsync_Success_StoresTransactionSetsCookieAndRedirects()
    {
        var services = new ServiceCollection();
        // The in-process value set expires entries by its TimeProvider, so it shares the handler's fixed clock.
        services.AddSingleton<TimeProvider>(new MutableTimeProvider(Now));
        services
            .AddSharedStateServices()
            .AddInProcessExpiringValueSet()
            .AddInProcessExpiringIndexSet();
        await using var provider = services.BuildServiceProvider();
        var values = provider.GetRequiredService<IExpiringValueSet>();
        var indexes = provider.GetRequiredService<IExpiringIndexSet>();
        var transactionService = new BffLoginTransactionService(
            new DefaultBffSessionStore(values, indexes)
        );

        var httpContext = CreateHttpContext("?returnUrl=/app");
        var authorizationUri = new Uri("https://op.example.com/authorize?client_id=bff");
        AuthorizationRequest? captured = null;
        _client
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

        await CreateHandler(CreateOptions(), transactionService).HandleAsync(httpContext);

        Assert.Equal(StatusCodes.Status302Found, httpContext.Response.StatusCode);
        Assert.Equal(
            authorizationUri.AbsoluteUri,
            httpContext.Response.Headers.Location.ToString()
        );
        var setCookie = httpContext.Response.Headers.SetCookie.ToString();
        var cookiePrefix = BffDefaults.LoginCookieName + "=";
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
        Assert.Equal("jkt", transaction.DPoPJwkThumbprint);
        Assert.Equal(captured.Nonce, transaction.Nonce);
    }

    #endregion
}
