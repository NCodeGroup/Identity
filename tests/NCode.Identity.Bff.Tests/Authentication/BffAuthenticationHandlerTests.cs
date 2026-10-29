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
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Buffers;
using NCode.Identity.Bff.Authentication;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.Jose;
using Xunit;

namespace NCode.Identity.Bff.Tests.Authentication;

public class BffAuthenticationHandlerTests : BaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 2, 5, 0, 0, TimeSpan.Zero);

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
                CreatedAt = Now,
                LastAccessedAt = Now,
                AbsoluteExpiresAt = Now.AddHours(8),
                IdleExpiresAt = Now.AddHours(1),
            },
            new BffSessionPayload
            {
                IdToken = "id-token",
                LogoutGuard = "guard",
                Claims =
                [
                    new BffStoredClaim { Type = JoseClaimNames.Payload.Sub, Value = "sub-1" },
                ],
            }
        );

    private async Task<(
        BffAuthenticationHandler Handler,
        DefaultHttpContext HttpContext
    )> CreateHandlerAsync(
        BffSessionState? session,
        object? endpointMetadata = null,
        BffOptions? bffOptions = null
    )
    {
        var options = Microsoft.Extensions.Options.Options.Create(bffOptions ?? new BffOptions());
        var timeProvider = new MutableTimeProvider(Now);
        var cookies = new BffCookies(options, timeProvider);
        var manager = CreateStrictMock<IBffSessionManager>();
        var resolver = new BffSessionResolver(manager.Object, cookies, [], options, timeProvider);

        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        var feature = new BffRequestFeature
        {
            IsResolved = true,
            Session = session,
            Principal = session is null
                ? null
                : BffSessionPrincipal.Create(session.Payload.Claims, session.Metadata.Issuer),
        };
        if (endpointMetadata is not null)
        {
            var endpoint = new Endpoint(
                null,
                new EndpointMetadataCollection(endpointMetadata),
                "test"
            );
            httpContext.SetEndpoint(endpoint);
            feature.CheckedEndpoint = endpoint;
        }

        httpContext.Features.Set(feature);

        var schemeOptions = CreateStrictMock<IOptionsMonitor<AuthenticationSchemeOptions>>();
        schemeOptions
            .Setup(x => x.Get(It.IsAny<string>()))
            .Returns(new AuthenticationSchemeOptions())
            .Verifiable();

        var handler = new BffAuthenticationHandler(
            schemeOptions.Object,
            NullLoggerFactory.Instance,
            UrlEncoder.Default,
            resolver,
            options
        );
        var scheme = new AuthenticationScheme(
            BffDefaults.AuthenticationScheme,
            null,
            typeof(BffAuthenticationHandler)
        );
        await handler.InitializeAsync(scheme, httpContext);
        return (handler, httpContext);
    }

    private static async Task<string> ReadBodyAsync(HttpContext httpContext)
    {
        httpContext.Response.Body.Position = 0;
        using var reader = new StreamReader(httpContext.Response.Body, SecureEncoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    #region AuthenticateAsync Tests

    [Fact]
    public async Task AuthenticateAsync_WithSession_Succeeds()
    {
        var (handler, _) = await CreateHandlerAsync(CreateState());

        var result = await handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(BffDefaults.AuthenticationScheme, result.Ticket?.AuthenticationScheme);
        Assert.Equal("sub-1", result.Principal?.FindFirstValue(JoseClaimNames.Payload.Sub));
    }

    [Fact]
    public async Task AuthenticateAsync_NoSession_ReturnsNoResult()
    {
        var (handler, _) = await CreateHandlerAsync(null);

        var result = await handler.AuthenticateAsync();

        Assert.True(result.None);
    }

    #endregion

    #region ChallengeAsync Tests

    [Fact]
    public async Task ChallengeAsync_ApiEndpoint_WritesUnauthorizedProblem()
    {
        var (handler, httpContext) = await CreateHandlerAsync(null, new BffApiAttribute());

        await handler.ChallengeAsync(null);

        Assert.Equal(StatusCodes.Status401Unauthorized, httpContext.Response.StatusCode);
        Assert.Equal("application/problem+json", httpContext.Response.ContentType);
        var body = await ReadBodyAsync(httpContext);
        Assert.Contains(BffProblemTypes.SessionRequired, body);
    }

    [Fact]
    public async Task ChallengeAsync_PageEndpoint_AllowedPath_RedirectsToLoginWithReturnUrl()
    {
        var bffOptions = new BffOptions { ReturnUrls = ["/app"] };
        var (handler, httpContext) = await CreateHandlerAsync(null, bffOptions: bffOptions);
        httpContext.Request.Path = "/app/page";
        httpContext.Request.QueryString = new QueryString("?a=1");

        await handler.ChallengeAsync(null);

        Assert.Equal(StatusCodes.Status302Found, httpContext.Response.StatusCode);
        Assert.Equal(
            "/bff/login?returnUrl=%2Fapp%2Fpage%3Fa%3D1",
            httpContext.Response.Headers.Location.ToString()
        );
    }

    [Fact]
    public async Task ChallengeAsync_PageEndpoint_DisallowedPath_RedirectsToLoginWithRoot()
    {
        var (handler, httpContext) = await CreateHandlerAsync(null);
        httpContext.Request.Path = "/other";

        await handler.ChallengeAsync(null);

        Assert.Equal(StatusCodes.Status302Found, httpContext.Response.StatusCode);
        Assert.Equal("/bff/login?returnUrl=%2F", httpContext.Response.Headers.Location.ToString());
    }

    #endregion

    #region ForbidAsync Tests

    [Fact]
    public async Task ForbidAsync_ApiEndpoint_WritesForbiddenProblem()
    {
        var (handler, httpContext) = await CreateHandlerAsync(null, new BffApiAttribute());

        await handler.ForbidAsync(null);

        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
        var body = await ReadBodyAsync(httpContext);
        Assert.Contains(BffProblemTypes.Forbidden, body);
    }

    #endregion
}
