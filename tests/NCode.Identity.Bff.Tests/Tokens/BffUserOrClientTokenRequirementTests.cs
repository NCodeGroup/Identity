// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.Bff.Tests.Frontends;
using NCode.Identity.Bff.Tokens;
using Xunit;

namespace NCode.Identity.Bff.Tests.Tokens;

public class BffUserOrClientTokenRequirementTests : BaseTests
{
    private static DateTimeOffset Now { get; } = new(2026, 1, 2, 5, 0, 0, TimeSpan.Zero);

    private static BffTokenContexts CreateContexts(Mock<IBffSessionManager> manager)
    {
        var timeProvider = new FakeTimeProvider(Now);
        var cookies = new BffCookies(timeProvider);
        var frontends = BffTestFrontends.CreateCatalog();
        var resolver = new BffSessionResolver(manager.Object, cookies, [], frontends, timeProvider);
        return new BffTokenContexts(frontends, resolver, cookies);
    }

    private static DefaultHttpContext CreateHttpContext(bool hasSession)
    {
        var session = hasSession
            ? new BffSessionState(
                new string('a', 43),
                "store-key",
                1,
                new BffSessionMetadata
                {
                    Frontend = BffTestFrontends.Name,
                    Authority = BffTestFrontends.Authority,
                    ClientId = BffTestFrontends.ClientId,
                    Issuer = "https://op.example",
                    Subject = "sub-1",
                    CreatedAt = Now.AddHours(-1),
                    LastAccessedAt = Now.AddMinutes(-1),
                    AbsoluteExpiresAt = Now.AddHours(7),
                    IdleExpiresAt = Now.AddMinutes(59),
                },
                new BffSessionPayload { IdToken = "id-token", LogoutGuard = "guard" }
            )
            : null;
        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set(new BffRequestFeature { IsResolved = true, Session = session });
        return httpContext;
    }

    private IBffTokenContext CreateContext(HttpContext httpContext)
    {
        var sessionManager = CreateStrictMock<IBffSessionManager>();
        return CreateContexts(sessionManager).ForRequest(httpContext);
    }

    private static BffTokenResult Token { get; } =
        BffTokenResult.Success(new BffAccessToken("access", "Bearer", Now.AddMinutes(5)));

    private Mock<IBffUserTokenService> MockUserService { get; }
    private Mock<IBffClientTokenService> MockClientService { get; }
    private BffUserOrClientTokenRequirement Requirement { get; }

    public BffUserOrClientTokenRequirementTests()
    {
        MockUserService = CreateStrictMock<IBffUserTokenService>();
        MockClientService = CreateStrictMock<IBffClientTokenService>();
        Requirement = new BffUserOrClientTokenRequirement(
            MockUserService.Object,
            MockClientService.Object
        );
    }

    private static BffTokenRequest CreateRequest() =>
        new(BffTokenRequirementNames.UserOrClient)
        {
            Resource = "https://api.example",
            Scope = "read",
        };

    #region Name Tests

    [Fact]
    public void Name_IsUserOrClient() =>
        Assert.Equal(BffTokenRequirementNames.UserOrClient, Requirement.Name);

    #endregion

    #region GetAccessTokenAsync Tests

    [Fact]
    public async Task GetAccessTokenAsync_Session_ReturnsUserToken()
    {
        var context = CreateContext(CreateHttpContext(true));
        var request = CreateRequest();
        MockUserService
            .Setup(x =>
                x.GetAccessTokenAsync(It.IsAny<IBffSessionScope>(), request, CancellationToken.None)
            )
            .ReturnsAsync(Token)
            .Verifiable();

        var result = await Requirement.GetAccessTokenAsync(
            context,
            request,
            CancellationToken.None
        );

        Assert.Same(Token, result);
    }

    [Fact]
    public async Task GetAccessTokenAsync_SessionUserTokenFails_DoesNotFallBackToClient()
    {
        var context = CreateContext(CreateHttpContext(true));
        var request = CreateRequest();
        var failure = BffTokenResult.Failure(
            StatusCodes.Status401Unauthorized,
            BffTokenFailureReasons.RefreshFailed
        );
        MockUserService
            .Setup(x =>
                x.GetAccessTokenAsync(It.IsAny<IBffSessionScope>(), request, CancellationToken.None)
            )
            .ReturnsAsync(failure)
            .Verifiable();

        var result = await Requirement.GetAccessTokenAsync(
            context,
            request,
            CancellationToken.None
        );

        Assert.Same(failure, result);
    }

    [Fact]
    public async Task GetAccessTokenAsync_NoSession_ReturnsClientToken()
    {
        var request = CreateRequest();
        var context = CreateContext(CreateHttpContext(false));
        MockClientService
            .Setup(x =>
                x.GetAccessTokenAsync(It.IsAny<IBffSessionScope>(), request, CancellationToken.None)
            )
            .ReturnsAsync(Token)
            .Verifiable();

        var result = await Requirement.GetAccessTokenAsync(
            context,
            request,
            CancellationToken.None
        );

        Assert.Same(Token, result);
    }

    [Fact]
    public async Task GetAccessTokenAsync_NullContext_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            Requirement.GetAccessTokenAsync(null!, CreateRequest(), CancellationToken.None).AsTask()
        );
    }

    #endregion
}
