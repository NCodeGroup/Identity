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

public class BffExchangeTokenRequirementTests : BaseTests
{
    private static BffTokenContexts CreateContexts(Mock<IBffSessionManager> manager)
    {
        var timeProvider = new FakeTimeProvider(
            new DateTimeOffset(2026, 1, 2, 5, 0, 0, TimeSpan.Zero)
        );
        var cookies = new BffCookies(timeProvider);
        var frontends = BffTestFrontends.CreateCatalog();
        var resolver = new BffSessionResolver(manager.Object, cookies, [], frontends, timeProvider);
        return new BffTokenContexts(frontends, resolver, cookies);
    }

    private IBffTokenContext CreateContext(HttpContext httpContext)
    {
        var sessionManager = CreateStrictMock<IBffSessionManager>();
        return CreateContexts(sessionManager).ForRequest(httpContext);
    }

    private Mock<IBffExchangeTokenService> MockService { get; }
    private BffExchangeTokenRequirement Requirement { get; }

    public BffExchangeTokenRequirementTests()
    {
        MockService = CreateStrictMock<IBffExchangeTokenService>();
        Requirement = new BffExchangeTokenRequirement(MockService.Object);
    }

    #region Name Tests

    [Fact]
    public void Name_IsExchange() =>
        Assert.Equal(BffTokenRequirementNames.Exchange, Requirement.Name);

    #endregion

    #region GetAccessTokenAsync Tests

    [Fact]
    public async Task GetAccessTokenAsync_Valid_ReturnsExchangedToken()
    {
        var httpContext = new DefaultHttpContext();
        var request = new BffTokenRequest(BffTokenRequirementNames.Exchange)
        {
            Resource = "https://api.example",
            Scope = "read",
        };
        var expected = BffTokenResult.Success(
            new BffAccessToken("exchanged", "Bearer", DateTimeOffset.UnixEpoch)
        );
        MockService
            .Setup(x =>
                x.GetAccessTokenAsync(
                    It.Is<IBffSessionScope>(s => s.HttpContext == httpContext),
                    request,
                    CancellationToken.None
                )
            )
            .ReturnsAsync(expected)
            .Verifiable();

        var result = await Requirement.GetAccessTokenAsync(
            CreateContext(httpContext),
            request,
            CancellationToken.None
        );

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task GetAccessTokenAsync_NullRequest_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            Requirement
                .GetAccessTokenAsync(
                    CreateContext(new DefaultHttpContext()),
                    null!,
                    CancellationToken.None
                )
                .AsTask()
        );
    }

    #endregion
}
