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
using Moq;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Endpoints;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Sessions;
using Xunit;

namespace NCode.Identity.Bff.Tests.Sessions;

public class BffSessionResolverTests : BaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 2, 5, 0, 0, TimeSpan.Zero);

    private static readonly string Handle = new('a', 43);

    private Mock<IBffSessionManager> MockManager { get; }
    private BffSessionResolver Resolver { get; }

    public BffSessionResolverTests()
    {
        MockManager = CreateStrictMock<IBffSessionManager>();
        var options = Microsoft.Extensions.Options.Options.Create(new BffOptions());
        var timeProvider = new MutableTimeProvider(Now);
        var cookies = new BffCookies(options, timeProvider);
        Resolver = new BffSessionResolver(MockManager.Object, cookies, [], options, timeProvider);
    }

    private static DefaultHttpContext CreateHttpContext(
        bool withFeature = true,
        bool withCookie = true,
        object? endpointMetadata = null,
        bool checkedEndpoint = true
    )
    {
        var httpContext = new DefaultHttpContext();
        if (withCookie)
            httpContext.Request.Headers.Cookie = $"{BffDefaults.SessionCookieName}={Handle}";

        var feature = new BffRequestFeature();
        if (endpointMetadata is not null)
        {
            var endpoint = new Endpoint(
                null,
                new EndpointMetadataCollection(endpointMetadata),
                "test"
            );
            httpContext.SetEndpoint(endpoint);
            if (checkedEndpoint)
                feature.CheckedEndpoint = endpoint;
        }

        if (withFeature)
            httpContext.Features.Set(feature);

        return httpContext;
    }

    private static BffSessionState CreateState(
        DateTimeOffset absoluteExpiresAt,
        DateTimeOffset idleExpiresAt
    ) =>
        new(
            Handle,
            "store-key",
            1,
            new BffSessionMetadata
            {
                Frontend = BffDefaults.FrontendName,
                Issuer = "https://op.example",
                Subject = "sub-1",
                CreatedAt = Now.AddHours(-1),
                LastAccessedAt = Now.AddMinutes(-5),
                AbsoluteExpiresAt = absoluteExpiresAt,
                IdleExpiresAt = idleExpiresAt,
            },
            new BffSessionPayload
            {
                IdToken = "id-token",
                LogoutGuard = "guard",
                Claims = [new BffStoredClaim { Type = "sub", Value = "sub-1" }],
            }
        );

    #region ResolveAsync Tests

    [Fact]
    public async Task ResolveAsync_NoFeature_Throws()
    {
        var httpContext = CreateHttpContext(withFeature: false);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Resolver.ResolveAsync(httpContext, CancellationToken.None)
        );
    }

    [Fact]
    public async Task ResolveAsync_UncheckedProtectedEndpoint_Throws()
    {
        var httpContext = CreateHttpContext(
            endpointMetadata: BffUserEndpointMetadata.Instance,
            checkedEndpoint: false
        );

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Resolver.ResolveAsync(httpContext, CancellationToken.None)
        );
    }

    [Fact]
    public async Task ResolveAsync_NoCookie_ReturnsNull()
    {
        var httpContext = CreateHttpContext(withCookie: false);

        var result = await Resolver.ResolveAsync(httpContext, CancellationToken.None);

        Assert.Null(result);
        Assert.True(httpContext.Features.Get<BffRequestFeature>()?.IsResolved);
    }

    [Fact]
    public async Task ResolveAsync_MissingRecord_DeletesCookie()
    {
        var httpContext = CreateHttpContext();
        MockManager
            .Setup(x => x.LoadAsync(Handle, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BffSessionState?)null)
            .Verifiable();

        var result = await Resolver.ResolveAsync(httpContext, CancellationToken.None);

        Assert.Null(result);
        var setCookie = Assert.Single(httpContext.Response.Headers.SetCookie.ToArray());
        Assert.StartsWith($"{BffDefaults.SessionCookieName}=;", setCookie);
    }

    [Fact]
    public async Task ResolveAsync_AbsoluteExpired_EndsSession()
    {
        var httpContext = CreateHttpContext();
        var state = CreateState(Now, Now.AddHours(1));
        MockManager
            .Setup(x => x.LoadAsync(Handle, It.IsAny<CancellationToken>()))
            .ReturnsAsync(state)
            .Verifiable();
        MockManager
            .Setup(x =>
                x.EndAsync(
                    httpContext,
                    state.StoreKey,
                    state.Metadata,
                    BffSessionEndReasons.AbsoluteExpiry,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Resolver.ResolveAsync(httpContext, CancellationToken.None);

        Assert.Null(result);
        Assert.Single(httpContext.Response.Headers.SetCookie.ToArray());
    }

    [Fact]
    public async Task ResolveAsync_IdleExpired_EndsSession()
    {
        var httpContext = CreateHttpContext();
        var state = CreateState(Now.AddHours(5), Now);
        MockManager
            .Setup(x => x.LoadAsync(Handle, It.IsAny<CancellationToken>()))
            .ReturnsAsync(state)
            .Verifiable();
        MockManager
            .Setup(x =>
                x.EndAsync(
                    httpContext,
                    state.StoreKey,
                    state.Metadata,
                    BffSessionEndReasons.IdleTimeout,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Resolver.ResolveAsync(httpContext, CancellationToken.None);

        Assert.Null(result);
        Assert.Single(httpContext.Response.Headers.SetCookie.ToArray());
    }

    [Fact]
    public async Task ResolveAsync_ValidSession_IsMemoized()
    {
        var httpContext = CreateHttpContext();
        var state = CreateState(Now.AddHours(5), Now.AddHours(1));
        MockManager
            .Setup(x => x.LoadAsync(Handle, It.IsAny<CancellationToken>()))
            .ReturnsAsync(state)
            .Verifiable();

        var first = await Resolver.ResolveAsync(httpContext, CancellationToken.None);
        var second = await Resolver.ResolveAsync(httpContext, CancellationToken.None);

        Assert.Same(state, first);
        Assert.Same(state, second);
        MockManager.Verify(x => x.LoadAsync(Handle, It.IsAny<CancellationToken>()), Times.Once);
        var sessionFeature = httpContext.Features.Get<IBffSessionFeature>();
        Assert.NotNull(sessionFeature);
        Assert.Equal("sub-1", sessionFeature.Metadata.Subject);
    }

    #endregion
}
