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
using Microsoft.Extensions.Logging;
using Moq;
using NCode.Identity.Bff.Client;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Logout;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.OpenId.Client;
using Xunit;

namespace NCode.Identity.Bff.Tests.Logout;

public class BffLogoutHandlerTests : BaseTests
{
    private const string Guard = "guard-value";

    private static readonly DateTimeOffset Now = new(2026, 1, 2, 5, 0, 0, TimeSpan.Zero);

    private readonly Mock<BffSessionResolver> _resolver;
    private readonly Mock<BffLogoutService> _logoutService;
    private readonly Mock<IProviderMetadataResolver> _metadataResolver;

    public BffLogoutHandlerTests()
    {
        var options = CreateOptions();
        var timeProvider = new MutableTimeProvider(Now);
        var cookies = new BffCookies(options, timeProvider);
        _resolver = CreateStrictMock<BffSessionResolver>(
            CreateStrictMock<IBffSessionManager>().Object,
            cookies,
            Array.Empty<IBffSessionValidator>(),
            options,
            timeProvider
        );
        _logoutService = CreateStrictMock<BffLogoutService>(
            CreateStrictMock<IBffSessionManager>().Object,
            CreateStrictMock<ITokenRevocationClient>().Object,
            new BffClientRegistrationProvider(options),
            cookies,
            CreateLooseMock<ILogger<BffLogoutService>>().Object
        );
        _metadataResolver = CreateStrictMock<IProviderMetadataResolver>();
    }

    private static Microsoft.Extensions.Options.IOptions<BffOptions> CreateOptions() =>
        Microsoft.Extensions.Options.Options.Create(
            new BffOptions
            {
                Authority = "https://op.example",
                ClientId = "bff",
                ReturnUrls = ["/"],
            }
        );

    private BffLogoutHandler CreateHandler()
    {
        var options = CreateOptions();
        return new BffLogoutHandler(
            options,
            _resolver.Object,
            _logoutService.Object,
            _metadataResolver.Object,
            new BffClientRegistrationProvider(options)
        );
    }

    private static BffSessionState CreateState() =>
        new(
            new string('a', 43),
            "store-key",
            1,
            new BffSessionMetadata
            {
                Frontend = BffDefaults.FrontendName,
                Issuer = "https://op.example",
                Subject = "sub-1",
                SessionId = "sid-1",
                CreatedAt = Now,
                LastAccessedAt = Now,
                AbsoluteExpiresAt = Now.AddHours(8),
                IdleExpiresAt = Now.AddHours(1),
            },
            new BffSessionPayload { IdToken = "id-token", LogoutGuard = Guard }
        );

    private static DefaultHttpContext CreateHttpContext(string query)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString(query);
        httpContext.Response.Body = new MemoryStream();
        return httpContext;
    }

    private void SetupResolve(BffSessionState? state) =>
        _resolver
            .Setup(x => x.ResolveAsync(It.IsAny<HttpContext>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(state))
            .Verifiable();

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_DisallowedReturnUrl_WritesBadRequest()
    {
        var httpContext = CreateHttpContext("?returnUrl=https%3A%2F%2Fevil.example%2F");
        var handler = CreateHandler();

        await handler.HandleAsync(httpContext);

        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_NoSession_RedirectsToReturnUrl()
    {
        SetupResolve(null);
        var httpContext = CreateHttpContext("?returnUrl=%2Fhome");
        var handler = CreateHandler();

        await handler.HandleAsync(httpContext);

        Assert.Equal(StatusCodes.Status302Found, httpContext.Response.StatusCode);
        Assert.Equal("/home", httpContext.Response.Headers.Location.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("?sid=wrong-value")]
    [InlineData("?sid=guard-value&sid=guard-value")]
    public async Task HandleAsync_BadSid_WritesBadRequestAndKeepsSession(string query)
    {
        SetupResolve(CreateState());
        var httpContext = CreateHttpContext(query);
        var handler = CreateHandler();

        await handler.HandleAsync(httpContext);

        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_MatchingSid_EndsSessionAndRedirects()
    {
        var state = CreateState();
        SetupResolve(state);
        var httpContext = CreateHttpContext("?sid=guard-value");
        _logoutService
            .Setup(x =>
                x.EndSessionAsync(
                    httpContext,
                    state,
                    BffSessionEndReasons.LoggedOut,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        var handler = CreateHandler();

        await handler.HandleAsync(httpContext);

        Assert.Equal(StatusCodes.Status302Found, httpContext.Response.StatusCode);
        Assert.Equal("/", httpContext.Response.Headers.Location.ToString());
    }

    #endregion
}
