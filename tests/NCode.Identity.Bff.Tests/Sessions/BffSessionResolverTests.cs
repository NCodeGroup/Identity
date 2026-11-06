// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Endpoints;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.Bff.Tests.Frontends;
using NCode.Identity.Jose;
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
        var timeProvider = new FakeTimeProvider(Now);
        var cookies = new BffCookies(timeProvider);
        Resolver = new BffSessionResolver(
            MockManager.Object,
            cookies,
            [],
            BffTestFrontends.CreateCatalog(),
            timeProvider
        );
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
            httpContext.Request.Headers.Cookie = $"{BffTestFrontends.SessionCookieName}={Handle}";

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

    private BffSessionResolver CreateResolver(
        IEnumerable<IBffSessionValidator> validators,
        BffFrontendCatalog? catalog = null
    )
    {
        var timeProvider = new FakeTimeProvider(Now);
        var cookies = new BffCookies(timeProvider);
        return new BffSessionResolver(
            MockManager.Object,
            cookies,
            validators,
            catalog ?? BffTestFrontends.CreateCatalog(),
            timeProvider
        );
    }

    private static BffSessionState CreateState(
        DateTimeOffset absoluteExpiresAt,
        DateTimeOffset idleExpiresAt,
        DateTimeOffset? lastAccessedAt = null,
        string frontend = BffTestFrontends.Name
    ) =>
        new(
            Handle,
            "store-key",
            1,
            new BffSessionMetadata
            {
                Frontend = frontend,
                Issuer = "https://op.example",
                Subject = "sub-1",
                CreatedAt = Now.AddHours(-1),
                LastAccessedAt = lastAccessedAt ?? Now.AddMinutes(-10),
                AbsoluteExpiresAt = absoluteExpiresAt,
                IdleExpiresAt = idleExpiresAt,
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
    public async Task ResolveAsync_UncheckedPageEndpoint_Throws()
    {
        var httpContext = CreateHttpContext(
            endpointMetadata: new BffPageAttribute(),
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
        Assert.StartsWith($"{BffTestFrontends.SessionCookieName}=;", setCookie);
    }

    [Fact]
    public async Task ResolveAsync_NoFrontendSelected_ReturnsNullWithoutLoading()
    {
        var catalog = BffTestFrontends.CreateCatalog(x =>
            x.Selection = new BffFrontendSelectionOptions
            {
                Rule = BffFrontendSelectorNames.Path,
                Values = ["/admin"],
            }
        );
        var httpContext = CreateHttpContext();
        httpContext.Request.Path = "/api";
        var resolver = CreateResolver([], catalog);

        var result = await resolver.ResolveAsync(httpContext, CancellationToken.None);

        Assert.Null(result);
        Assert.True(httpContext.Features.Get<BffRequestFeature>()?.IsResolved);
        Assert.Empty(httpContext.Response.Headers.SetCookie.ToArray());
    }

    [Fact]
    public async Task ResolveAsync_SessionOfAnotherFrontend_DeletesCookie()
    {
        var httpContext = CreateHttpContext();
        var state = CreateState(Now.AddHours(5), Now.AddHours(1), frontend: "other");
        MockManager
            .Setup(x => x.LoadAsync(Handle, It.IsAny<CancellationToken>()))
            .ReturnsAsync(state)
            .Verifiable();

        var result = await Resolver.ResolveAsync(httpContext, CancellationToken.None);

        Assert.Null(result);
        Assert.Null(httpContext.Features.Get<BffRequestFeature>()?.Session);
        var setCookie = Assert.Single(httpContext.Response.Headers.SetCookie.ToArray());
        Assert.StartsWith($"{BffTestFrontends.SessionCookieName}=;", setCookie);
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

    [Fact]
    public async Task ResolveAsync_RejectingValidator_EndsSession()
    {
        var httpContext = CreateHttpContext();
        var state = CreateState(Now.AddHours(5), Now.AddHours(1));
        var validator = CreateStrictMock<IBffSessionValidator>();
        validator
            .Setup(x =>
                x.ValidateAsync(
                    It.IsAny<BffSessionValidationContext>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(false)
            .Verifiable();
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
                    BffSessionEndReasons.EndedByValidator,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        var resolver = CreateResolver([validator.Object]);

        var result = await resolver.ResolveAsync(httpContext, CancellationToken.None);

        Assert.Null(result);
        Assert.Single(httpContext.Response.Headers.SetCookie.ToArray());
    }

    [Fact]
    public async Task ResolveAsync_ApiRequestPastThreshold_SlidesSession()
    {
        var httpContext = CreateHttpContext(endpointMetadata: new BffApiAttribute());
        var state = CreateState(Now.AddHours(5), Now.AddHours(1));
        BffSessionState? slidState = null;
        MockManager
            .Setup(x => x.LoadAsync(Handle, It.IsAny<CancellationToken>()))
            .ReturnsAsync(state)
            .Verifiable();
        MockManager
            .Setup(x =>
                x.UpdateAsync(
                    state,
                    It.IsAny<Func<BffSessionState, BffSessionState?>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                (
                    BffSessionState current,
                    Func<BffSessionState, BffSessionState?> update,
                    CancellationToken _
                ) => slidState = update(current)
            )
            .Verifiable();

        var result = await Resolver.ResolveAsync(httpContext, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(Now, result.Metadata.LastAccessedAt);
        Assert.Equal(Now.AddHours(1), result.Metadata.IdleExpiresAt);
        Assert.Same(slidState, result);
    }

    [Fact]
    public async Task ResolveAsync_PageRequestPastThreshold_SlidesSession()
    {
        var httpContext = CreateHttpContext(endpointMetadata: new BffPageAttribute());
        var state = CreateState(Now.AddHours(5), Now.AddHours(1));
        BffSessionState? slidState = null;
        MockManager
            .Setup(x => x.LoadAsync(Handle, It.IsAny<CancellationToken>()))
            .ReturnsAsync(state)
            .Verifiable();
        MockManager
            .Setup(x =>
                x.UpdateAsync(
                    state,
                    It.IsAny<Func<BffSessionState, BffSessionState?>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                (
                    BffSessionState current,
                    Func<BffSessionState, BffSessionState?> update,
                    CancellationToken _
                ) => slidState = update(current)
            )
            .Verifiable();

        var result = await Resolver.ResolveAsync(httpContext, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(Now, result.Metadata.LastAccessedAt);
        Assert.Equal(Now.AddHours(1), result.Metadata.IdleExpiresAt);
        Assert.Same(slidState, result);
    }

    [Fact]
    public async Task ResolveAsync_FrontendWithLongerIdleTimeout_SlidesByItsIdleTimeout()
    {
        var catalog = BffTestFrontends.CreateCatalog(x =>
            x.Session.IdleTimeout = TimeSpan.FromHours(2)
        );
        var httpContext = CreateHttpContext(endpointMetadata: new BffApiAttribute());
        var state = CreateState(Now.AddHours(5), Now.AddHours(1), Now.AddMinutes(-30));
        MockManager
            .Setup(x => x.LoadAsync(Handle, It.IsAny<CancellationToken>()))
            .ReturnsAsync(state)
            .Verifiable();
        MockManager
            .Setup(x =>
                x.UpdateAsync(
                    state,
                    It.IsAny<Func<BffSessionState, BffSessionState?>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                (
                    BffSessionState current,
                    Func<BffSessionState, BffSessionState?> update,
                    CancellationToken _
                ) => update(current)
            )
            .Verifiable();
        var resolver = CreateResolver([], catalog);

        var result = await resolver.ResolveAsync(httpContext, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(Now, result.Metadata.LastAccessedAt);
        Assert.Equal(Now.AddHours(2), result.Metadata.IdleExpiresAt);
    }

    [Fact]
    public async Task ResolveAsync_ApiRequestUnderThreshold_DoesNotSlide()
    {
        var httpContext = CreateHttpContext(endpointMetadata: new BffApiAttribute());
        var state = CreateState(Now.AddHours(5), Now.AddHours(1), Now.AddMinutes(-1));
        MockManager
            .Setup(x => x.LoadAsync(Handle, It.IsAny<CancellationToken>()))
            .ReturnsAsync(state)
            .Verifiable();

        var result = await Resolver.ResolveAsync(httpContext, CancellationToken.None);

        Assert.Same(state, result);
    }

    [Fact]
    public async Task ResolveAsync_UserEndpointPastThreshold_DoesNotSlide()
    {
        var httpContext = CreateHttpContext(endpointMetadata: BffUserEndpointMetadata.Instance);
        var state = CreateState(Now.AddHours(5), Now.AddHours(1));
        MockManager
            .Setup(x => x.LoadAsync(Handle, It.IsAny<CancellationToken>()))
            .ReturnsAsync(state)
            .Verifiable();

        var result = await Resolver.ResolveAsync(httpContext, CancellationToken.None);

        Assert.Same(state, result);
    }

    [Fact]
    public async Task ResolveAsync_HubEndpoint_Slides()
    {
        var httpContext = CreateHttpContext(endpointMetadata: new BffHubAttribute());
        var state = CreateState(Now.AddHours(5), Now.AddHours(1));
        BffSessionState? slidState = null;
        MockManager
            .Setup(x => x.LoadAsync(Handle, It.IsAny<CancellationToken>()))
            .ReturnsAsync(state)
            .Verifiable();
        MockManager
            .Setup(x =>
                x.UpdateAsync(
                    state,
                    It.IsAny<Func<BffSessionState, BffSessionState?>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                (
                    BffSessionState current,
                    Func<BffSessionState, BffSessionState?> update,
                    CancellationToken _
                ) => slidState = update(current)
            )
            .Verifiable();

        var result = await Resolver.ResolveAsync(httpContext, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(Now, result.Metadata.LastAccessedAt);
        Assert.Same(slidState, result);
    }

    [Fact]
    public async Task ResolveAsync_WithSession_PrincipalCarriesLogoutClaims()
    {
        var httpContext = CreateHttpContext();
        httpContext.Request.PathBase = "/app";
        var state = CreateState(Now.AddHours(5), Now.AddHours(1));
        MockManager
            .Setup(x => x.LoadAsync(Handle, It.IsAny<CancellationToken>()))
            .ReturnsAsync(state)
            .Verifiable();

        await Resolver.ResolveAsync(httpContext, CancellationToken.None);

        var principal = httpContext.Features.Get<BffRequestFeature>()?.Principal;
        Assert.NotNull(principal);
        Assert.Equal("/app/bff/logout", principal.FindFirst(BffClaimTypes.LogoutUrl)?.Value);
        Assert.Equal("guard", principal.FindFirst(BffClaimTypes.LogoutSid)?.Value);
        var sessionFeature = httpContext.Features.Get<IBffSessionFeature>();
        Assert.NotNull(sessionFeature);
        Assert.Equal(
            "/app/bff/logout",
            sessionFeature.Principal.FindFirst(BffClaimTypes.LogoutUrl)?.Value
        );
    }

    #endregion

    #region ValidateAsync Tests

    [Fact]
    public async Task ValidateAsync_Expired_EndsSessionWithoutHttpContext()
    {
        var catalog = BffTestFrontends.CreateCatalog();
        var frontend = BffTestFrontends.Get(catalog);
        var state = CreateState(Now, Now.AddHours(1));
        MockManager
            .Setup(x => x.LoadAsync(Handle, It.IsAny<CancellationToken>()))
            .ReturnsAsync(state)
            .Verifiable();
        MockManager
            .Setup(x =>
                x.EndAsync(
                    null,
                    state.StoreKey,
                    state.Metadata,
                    BffSessionEndReasons.AbsoluteExpiry,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Resolver.ValidateAsync(
            frontend,
            Handle,
            null,
            null,
            CancellationToken.None
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateAsync_ActivityAfterThreshold_Slides()
    {
        var frontend = BffTestFrontends.Get(BffTestFrontends.CreateCatalog());
        var state = CreateState(Now.AddHours(5), Now.AddHours(1));
        MockManager
            .Setup(x => x.LoadAsync(Handle, It.IsAny<CancellationToken>()))
            .ReturnsAsync(state)
            .Verifiable();
        MockManager
            .Setup(x =>
                x.UpdateAsync(
                    state,
                    It.IsAny<Func<BffSessionState, BffSessionState?>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                (
                    BffSessionState current,
                    Func<BffSessionState, BffSessionState?> update,
                    CancellationToken _
                ) => update(current)
            )
            .Verifiable();

        var result = await Resolver.ValidateAsync(
            frontend,
            Handle,
            null,
            Now,
            CancellationToken.None
        );

        Assert.NotNull(result);
        Assert.Equal(Now, result.Metadata.LastAccessedAt);
        Assert.Equal(Now.AddHours(1), result.Metadata.IdleExpiresAt);
    }

    [Fact]
    public async Task ValidateAsync_NoActivity_DoesNotSlide()
    {
        var frontend = BffTestFrontends.Get(BffTestFrontends.CreateCatalog());
        var state = CreateState(Now.AddHours(5), Now.AddHours(1));
        MockManager
            .Setup(x => x.LoadAsync(Handle, It.IsAny<CancellationToken>()))
            .ReturnsAsync(state)
            .Verifiable();

        var result = await Resolver.ValidateAsync(
            frontend,
            Handle,
            null,
            null,
            CancellationToken.None
        );

        Assert.Same(state, result);
    }

    #endregion
}
