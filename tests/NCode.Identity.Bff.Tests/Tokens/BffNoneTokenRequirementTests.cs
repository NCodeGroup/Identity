// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.Bff.Tokens;
using Xunit;

namespace NCode.Identity.Bff.Tests.Tokens;

public class BffNoneTokenRequirementTests : BaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 2, 5, 0, 0, TimeSpan.Zero);

    private static BffSessionResolver CreateResolver(Mock<IBffSessionManager> manager)
    {
        var options = Microsoft.Extensions.Options.Options.Create(
            new BffOptions { Authority = "https://op.example", ClientId = "bff" }
        );
        var timeProvider = new FakeTimeProvider(Now);
        return new BffSessionResolver(
            manager.Object,
            new BffCookies(options, timeProvider),
            [],
            options,
            timeProvider
        );
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
                    Frontend = BffDefaults.FrontendName,
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

    private BffNoneTokenRequirement Requirement { get; }

    public BffNoneTokenRequirementTests()
    {
        Requirement = new BffNoneTokenRequirement(
            CreateResolver(CreateStrictMock<IBffSessionManager>())
        );
    }

    #region Name Tests

    [Fact]
    public void Name_IsNone() => Assert.Equal(BffTokenRequirementNames.None, Requirement.Name);

    #endregion

    #region GetAccessTokenAsync Tests

    [Fact]
    public async Task GetAccessTokenAsync_Session_ReturnsSuccessWithoutToken()
    {
        var result = await Requirement.GetAccessTokenAsync(
            CreateHttpContext(true),
            new BffTokenRequest(BffTokenRequirementNames.None),
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        Assert.Null(result.Token);
    }

    [Fact]
    public async Task GetAccessTokenAsync_NoSession_ReturnsSessionRequired()
    {
        var result = await Requirement.GetAccessTokenAsync(
            CreateHttpContext(false),
            new BffTokenRequest(BffTokenRequirementNames.None),
            CancellationToken.None
        );

        Assert.Equal(401, result.StatusCode);
        Assert.Equal(BffTokenFailureReasons.SessionRequired, result.Reason);
    }

    [Fact]
    public async Task GetAccessTokenAsync_NullHttpContext_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            Requirement
                .GetAccessTokenAsync(
                    null!,
                    new BffTokenRequest(BffTokenRequirementNames.None),
                    CancellationToken.None
                )
                .AsTask()
        );
    }

    #endregion
}
