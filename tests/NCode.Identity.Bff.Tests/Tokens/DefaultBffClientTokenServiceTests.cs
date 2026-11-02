// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Bff.Client;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Tokens;
using NCode.Identity.OpenId.Client;
using Xunit;

namespace NCode.Identity.Bff.Tests.Tokens;

public class DefaultBffClientTokenServiceTests : BaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 2, 5, 0, 0, TimeSpan.Zero);

    private Mock<ITokenClient> MockTokenClient { get; }
    private FakeTimeProvider TimeProvider { get; }
    private DefaultBffClientTokenService Service { get; }

    public DefaultBffClientTokenServiceTests()
    {
        var options = Microsoft.Extensions.Options.Options.Create(
            new BffOptions { Authority = "https://op.example", ClientId = "bff" }
        );
        MockTokenClient = CreateStrictMock<ITokenClient>();
        TimeProvider = new FakeTimeProvider(Now);
        var grantClient = new BffGrantClient(
            MockTokenClient.Object,
            new BffClientRegistrationProvider(options),
            TimeProvider,
            CreateLooseMock<ILogger<BffGrantClient>>().Object
        );
        Service = new DefaultBffClientTokenService(grantClient, options, TimeProvider);
    }

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

    #region GetAccessTokenAsync Tests

    [Fact]
    public async Task GetAccessTokenAsync_FirstRequest_RequestsClientCredentialsToken()
    {
        SetupIssue("https://api.example", "read", "client-access", 1);

        var result = await Service.GetAccessTokenAsync(
            "https://api.example",
            "read",
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        Assert.Equal("client-access", result.Token?.Value);
    }

    [Fact]
    public async Task GetAccessTokenAsync_FreshCachedToken_ReturnsItWithoutRequesting()
    {
        SetupIssue(null, null, "client-access", 1);
        await Service.GetAccessTokenAsync(null, null, CancellationToken.None);

        var result = await Service.GetAccessTokenAsync(null, null, CancellationToken.None);

        Assert.Equal("client-access", result.Token?.Value);
    }

    [Fact]
    public async Task GetAccessTokenAsync_StaleCachedToken_RequestsAgain()
    {
        SetupIssue(null, null, "client-access", 2);
        await Service.GetAccessTokenAsync(null, null, CancellationToken.None);
        // inside the default refresh-before-expiry window of the 300 second token
        TimeProvider.Advance(TimeSpan.FromSeconds(290));

        var result = await Service.GetAccessTokenAsync(null, null, CancellationToken.None);

        Assert.Equal("client-access", result.Token?.Value);
    }

    [Fact]
    public async Task GetAccessTokenAsync_DifferentResource_CachesSeparately()
    {
        SetupIssue("https://a.example", null, "access-a", 1);
        SetupIssue("https://b.example", null, "access-b", 1);

        var a = await Service.GetAccessTokenAsync(
            "https://a.example",
            null,
            CancellationToken.None
        );
        var b = await Service.GetAccessTokenAsync(
            "https://b.example",
            null,
            CancellationToken.None
        );

        Assert.Equal("access-a", a.Token?.Value);
        Assert.Equal("access-b", b.Token?.Value);
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

        var first = await Service.GetAccessTokenAsync(null, null, CancellationToken.None);
        var second = await Service.GetAccessTokenAsync(null, null, CancellationToken.None);

        Assert.Equal(502, first.StatusCode);
        Assert.Equal(502, second.StatusCode);
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

        var first = Service.GetAccessTokenAsync(null, null, CancellationToken.None).AsTask();
        var second = Service.GetAccessTokenAsync(null, null, CancellationToken.None).AsTask();
        release.SetResult(
            new TokenResult(new TokenResponse { AccessToken = "shared", ExpiresIn = 300 })
        );
        var results = await Task.WhenAll(first, second);

        Assert.All(results, r => Assert.Equal("shared", r.Token?.Value));
    }

    #endregion
}
