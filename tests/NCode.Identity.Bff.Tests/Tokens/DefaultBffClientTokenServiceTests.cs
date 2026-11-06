// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Tests.Frontends;
using NCode.Identity.Bff.Tokens;
using NCode.Identity.OpenId.Client;
using Xunit;

namespace NCode.Identity.Bff.Tests.Tokens;

public class DefaultBffClientTokenServiceTests : BaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 2, 5, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(30);

    private static BffTokenRequest DefaultRequest { get; } = new(BffTokenRequirementNames.Client);

    private Mock<ITokenClient> MockTokenClient { get; }
    private FakeTimeProvider TimeProvider { get; }
    private BffRefreshLocks Locks { get; } = new();
    private DefaultBffClientTokenService Service { get; }

    public DefaultBffClientTokenServiceTests()
    {
        MockTokenClient = CreateStrictMock<ITokenClient>();
        TimeProvider = new FakeTimeProvider(Now);
        Service = CreateService(TimeSpan.FromSeconds(40));
    }

    private DefaultBffClientTokenService CreateService(
        TimeSpan refreshLeaseDuration,
        BffFrontendCatalog? frontends = null
    )
    {
        var options = Microsoft.Extensions.Options.Options.Create(
            new BffOptions { Tokens = { RefreshLeaseDuration = refreshLeaseDuration } }
        );
        var grantClient = new BffGrantClient(
            MockTokenClient.Object,
            TimeProvider,
            CreateLooseMock<ILogger<BffGrantClient>>().Object
        );
        return new DefaultBffClientTokenService(
            grantClient,
            Locks,
            frontends ?? BffTestFrontends.CreateCatalog(),
            options,
            TimeProvider
        );
    }

    private static DefaultHttpContext CreateHttpContext(string host = "app.example.com")
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString(host);
        return httpContext;
    }

    private static BffTokenRequest CreateRequest(string? resource, string? scope) =>
        new(BffTokenRequirementNames.Client) { Resource = resource, Scope = scope };

    private TaskCompletionSource<TokenResult> SetupBlockedIssue(Times times)
    {
        var release = new TaskCompletionSource<TokenResult>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        MockTokenClient
            .Setup(x =>
                x.RequestTokenAsync(
                    It.IsAny<ClientRegistration>(),
                    It.IsAny<TokenRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                (ClientRegistration _, TokenRequest _, CancellationToken cancellationToken) =>
                    new ValueTask<TokenResult>(release.Task.WaitAsync(cancellationToken))
            )
            .Verifiable(times);
        return release;
    }

    private static TokenResult IssuedToken(string accessToken) =>
        new(new TokenResponse { AccessToken = accessToken, ExpiresIn = 300 });

    private void SetupIssue(string? resource, string? scope, string accessToken, int times) =>
        MockTokenClient
            .Setup(x =>
                x.RequestTokenAsync(
                    It.IsAny<ClientRegistration>(),
                    It.Is<TokenRequest>(r =>
                        r is ClientCredentialsTokenRequest
                        && r.Resources.SequenceEqual(
                            resource == null ? Array.Empty<string>() : new[] { resource }
                        )
                        && r.Scope == scope
                    ),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(
                new TokenResult(
                    new TokenResponse
                    {
                        AccessToken = accessToken,
                        TokenType = "Bearer",
                        ExpiresIn = 300,
                    }
                )
            )
            .Verifiable(Times.Exactly(times));

    private void SetupIssueForClient(string clientId, string accessToken) =>
        MockTokenClient
            .Setup(x =>
                x.RequestTokenAsync(
                    It.Is<ClientRegistration>(r => r.ClientId == clientId),
                    It.IsAny<TokenRequest>(),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(
                new TokenResult(
                    new TokenResponse
                    {
                        AccessToken = accessToken,
                        TokenType = "Bearer",
                        ExpiresIn = 300,
                    }
                )
            )
            .Verifiable(Times.Once);

    #region GetAccessTokenAsync Tests

    [Fact]
    public async Task GetAccessTokenAsync_FirstRequest_RequestsClientCredentialsToken()
    {
        SetupIssue("https://api.example", "read", "client-access", 1);

        var result = await Service.GetAccessTokenAsync(
            CreateHttpContext(),
            CreateRequest("https://api.example", "read"),
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        Assert.Equal("client-access", result.Token?.Value);
    }

    [Fact]
    public async Task GetAccessTokenAsync_FreshCachedToken_ReturnsItWithoutRequesting()
    {
        SetupIssue(null, null, "client-access", 1);
        await Service.GetAccessTokenAsync(
            CreateHttpContext(),
            DefaultRequest,
            CancellationToken.None
        );

        var result = await Service.GetAccessTokenAsync(
            CreateHttpContext(),
            DefaultRequest,
            CancellationToken.None
        );

        Assert.Equal("client-access", result.Token?.Value);
    }

    [Fact]
    public async Task GetAccessTokenAsync_StaleCachedToken_RequestsAgain()
    {
        SetupIssue(null, null, "client-access", 2);
        await Service.GetAccessTokenAsync(
            CreateHttpContext(),
            DefaultRequest,
            CancellationToken.None
        );
        // inside the default refresh-before-expiry window of the 300 second token
        TimeProvider.Advance(TimeSpan.FromSeconds(290));

        var result = await Service.GetAccessTokenAsync(
            CreateHttpContext(),
            DefaultRequest,
            CancellationToken.None
        );

        Assert.Equal("client-access", result.Token?.Value);
    }

    [Fact]
    public async Task GetAccessTokenAsync_DifferentResource_CachesSeparately()
    {
        SetupIssue("https://a.example", null, "access-a", 1);
        SetupIssue("https://b.example", null, "access-b", 1);

        var a = await Service.GetAccessTokenAsync(
            CreateHttpContext(),
            CreateRequest("https://a.example", null),
            CancellationToken.None
        );
        var b = await Service.GetAccessTokenAsync(
            CreateHttpContext(),
            CreateRequest("https://b.example", null),
            CancellationToken.None
        );

        Assert.Equal("access-a", a.Token?.Value);
        Assert.Equal("access-b", b.Token?.Value);
    }

    [Fact]
    public async Task GetAccessTokenAsync_TwoFrontends_RequestATokenForEach()
    {
        var service = CreateService(
            TimeSpan.FromSeconds(40),
            BffTestFrontends.CreateCatalog(
                new BffOptions
                {
                    Frontends =
                    {
                        ["shop"] = BffTestFrontends.CreateOptions(x =>
                        {
                            x.ClientId = "shop-client";
                            x.Selection = new BffFrontendSelectionOptions
                            {
                                Rule = BffFrontendSelectorNames.Host,
                                Values = ["shop.example.com"],
                            };
                        }),
                        ["web"] = BffTestFrontends.CreateOptions(x => x.ClientId = "web-client"),
                    },
                }
            )
        );
        SetupIssueForClient("shop-client", "shop-access");
        SetupIssueForClient("web-client", "web-access");

        var shop = await service.GetAccessTokenAsync(
            CreateHttpContext("shop.example.com"),
            DefaultRequest,
            CancellationToken.None
        );
        var web = await service.GetAccessTokenAsync(
            CreateHttpContext("web.example.com"),
            DefaultRequest,
            CancellationToken.None
        );

        Assert.Equal("shop-access", shop.Token?.Value);
        Assert.Equal("web-access", web.Token?.Value);
    }

    [Fact]
    public async Task GetAccessTokenAsync_NoFrontend_Answers401()
    {
        var service = CreateService(
            TimeSpan.FromSeconds(40),
            BffTestFrontends.CreateCatalog(
                new BffOptions
                {
                    Frontends =
                    {
                        ["shop"] = BffTestFrontends.CreateOptions(x =>
                            x.Selection = new BffFrontendSelectionOptions
                            {
                                Rule = BffFrontendSelectorNames.Host,
                                Values = ["shop.example.com"],
                            }
                        ),
                    },
                }
            )
        );

        var result = await service.GetAccessTokenAsync(
            CreateHttpContext("other.example.com"),
            DefaultRequest,
            CancellationToken.None
        );

        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal(BffTokenFailureReasons.SessionRequired, result.Reason);
    }

    [Fact]
    public async Task GetAccessTokenAsync_Refused_ReturnsFailureAndCachesNothing()
    {
        MockTokenClient
            .Setup(x =>
                x.RequestTokenAsync(
                    It.IsAny<ClientRegistration>(),
                    It.IsAny<TokenRequest>(),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(new TokenResult(new ProtocolError("invalid_client", 401)))
            .Verifiable(Times.Exactly(2));

        var first = await Service.GetAccessTokenAsync(
            CreateHttpContext(),
            DefaultRequest,
            CancellationToken.None
        );
        var second = await Service.GetAccessTokenAsync(
            CreateHttpContext(),
            DefaultRequest,
            CancellationToken.None
        );

        Assert.Equal(StatusCodes.Status502BadGateway, first.StatusCode);
        Assert.Equal(StatusCodes.Status502BadGateway, second.StatusCode);
    }

    [Fact]
    public async Task GetAccessTokenAsync_ConcurrentRequests_RequestOnce()
    {
        var release = new TaskCompletionSource<TokenResult>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        MockTokenClient
            .Setup(x =>
                x.RequestTokenAsync(
                    It.IsAny<ClientRegistration>(),
                    It.IsAny<TokenRequest>(),
                    CancellationToken.None
                )
            )
            .Returns(() => new ValueTask<TokenResult>(release.Task))
            .Verifiable(Times.Once);

        var first = Service
            .GetAccessTokenAsync(CreateHttpContext(), DefaultRequest, CancellationToken.None)
            .AsTask();
        var second = Service
            .GetAccessTokenAsync(CreateHttpContext(), DefaultRequest, CancellationToken.None)
            .AsTask();
        release.SetResult(
            new TokenResult(new TokenResponse { AccessToken = "shared", ExpiresIn = 300 })
        );
        var results = await Task.WhenAll(first, second);

        Assert.All(results, r => Assert.Equal("shared", r.Token?.Value));
    }

    [Fact]
    public async Task GetAccessTokenAsync_LockHeldPastLeaseDuration_ReturnsRefreshWaitTimeout()
    {
        var service = CreateService(TimeSpan.FromMilliseconds(50));
        var release = SetupBlockedIssue(Times.Once());
        var first = service
            .GetAccessTokenAsync(CreateHttpContext(), DefaultRequest, CancellationToken.None)
            .AsTask();

        var second = await service.GetAccessTokenAsync(
            CreateHttpContext(),
            DefaultRequest,
            CancellationToken.None
        );

        Assert.False(second.IsSuccess);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, second.StatusCode);
        Assert.Equal(BffTokenFailureReasons.RefreshWaitTimeout, second.Reason);

        release.SetResult(IssuedToken("client-access"));
        var result = await first.WaitAsync(WaitTimeout);

        Assert.Equal("client-access", result.Token?.Value);
        Assert.Equal(0, Locks.Count);
    }

    [Fact]
    public async Task GetAccessTokenAsync_HolderCanceled_NextCallerRequestsItself()
    {
        var release = SetupBlockedIssue(Times.Exactly(2));
        using var cts = new CancellationTokenSource();
        var first = Service
            .GetAccessTokenAsync(CreateHttpContext(), DefaultRequest, cts.Token)
            .AsTask();
        var second = Service
            .GetAccessTokenAsync(CreateHttpContext(), DefaultRequest, CancellationToken.None)
            .AsTask();

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first.WaitAsync(WaitTimeout));
        Assert.False(second.IsCompleted);

        release.SetResult(IssuedToken("client-access"));
        var result = await second.WaitAsync(WaitTimeout);

        Assert.Equal("client-access", result.Token?.Value);
        Assert.Equal(0, Locks.Count);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WaiterCanceled_ThrowsAndLeavesTheLockUsable()
    {
        var release = SetupBlockedIssue(Times.Once());
        var first = Service
            .GetAccessTokenAsync(CreateHttpContext(), DefaultRequest, CancellationToken.None)
            .AsTask();
        using var cts = new CancellationTokenSource();
        var waiter = Service
            .GetAccessTokenAsync(CreateHttpContext(), DefaultRequest, cts.Token)
            .AsTask();

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            waiter.WaitAsync(WaitTimeout)
        );

        release.SetResult(IssuedToken("client-access"));
        await first.WaitAsync(WaitTimeout);
        var cached = await Service.GetAccessTokenAsync(
            CreateHttpContext(),
            DefaultRequest,
            CancellationToken.None
        );

        Assert.Equal("client-access", cached.Token?.Value);
        Assert.Equal(0, Locks.Count);
    }

    [Fact]
    public async Task GetAccessTokenAsync_NullRequest_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            Service.GetAccessTokenAsync(CreateHttpContext(), null!, CancellationToken.None).AsTask()
        );
    }

    [Fact]
    public async Task GetAccessTokenAsync_NullHttpContext_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            Service.GetAccessTokenAsync(null!, DefaultRequest, CancellationToken.None).AsTask()
        );
    }

    #endregion
}
