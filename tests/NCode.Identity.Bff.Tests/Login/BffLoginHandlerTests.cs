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
using Microsoft.Extensions.Hosting;
using Moq;
using NCode.Identity.Bff.Auditing;
using NCode.Identity.Bff.Client;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Login;
using NCode.Identity.Bff.Options;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Client;
using Xunit;

namespace NCode.Identity.Bff.Tests.Login;

public class BffLoginHandlerTests : BaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly Mock<IAuthorizationRequestClient> _client;
    private readonly Mock<IBffAuditRecorder> _auditRecorder;
    private readonly Mock<IBffSessionStore> _store;

    public BffLoginHandlerTests()
    {
        _client = CreateStrictMock<IAuthorizationRequestClient>();
        _auditRecorder = CreateStrictMock<IBffAuditRecorder>();
        _store = CreateStrictMock<IBffSessionStore>();
    }

    private BffLoginHandler CreateHandler(BffOptions bffOptions)
    {
        var options = Microsoft.Extensions.Options.Options.Create(bffOptions);
        var timeProvider = new MutableTimeProvider(Now);
        return new BffLoginHandler(
            options,
            new BffClientRegistrationProvider(options),
            _client.Object,
            new BffLoginTransactionService(_store.Object),
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
                    new AuthorizationRequestResult(new ProtocolError("invalid_request", 400))
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

    #endregion
}
