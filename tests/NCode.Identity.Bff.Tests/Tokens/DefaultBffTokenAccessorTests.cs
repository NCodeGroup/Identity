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

public class DefaultBffTokenAccessorTests : BaseTests
{
    private const string OtherName = "other";

    private BffTokenContexts CreateContexts()
    {
        var timeProvider = new FakeTimeProvider(
            new DateTimeOffset(2026, 1, 2, 5, 0, 0, TimeSpan.Zero)
        );
        var cookies = new BffCookies(timeProvider);
        var frontends = BffTestFrontends.CreateCatalog();
        var resolver = new BffSessionResolver(
            CreateStrictMock<IBffSessionManager>().Object,
            cookies,
            [],
            frontends,
            timeProvider
        );
        return new BffTokenContexts(frontends, resolver, cookies);
    }

    private Mock<IBffTokenRequirement> CreateRequirement(string name)
    {
        var mock = CreateStrictMock<IBffTokenRequirement>();
        mock.SetupGet(x => x.Name).Returns(name).Verifiable();
        return mock;
    }

    private static DefaultHttpContext CreateGuardedHttpContext()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.SetEndpoint(
            new Endpoint(null, new EndpointMetadataCollection(new BffApiAttribute()), "test")
        );
        return httpContext;
    }

    #region GetAccessTokenAsync Tests

    [Fact]
    public async Task GetAccessTokenAsync_KnownName_DispatchesToRequirement()
    {
        var user = CreateRequirement(BffTokenRequirementNames.User);
        var other = CreateRequirement(OtherName);
        var httpContext = CreateGuardedHttpContext();
        var request = new BffTokenRequest(OtherName);
        var expected = BffTokenResult.Success(null);
        other
            .Setup(x =>
                x.GetAccessTokenAsync(
                    It.Is<IBffTokenContext>(c => c.HttpContext == httpContext),
                    request,
                    CancellationToken.None
                )
            )
            .Returns(new ValueTask<BffTokenResult>(expected))
            .Verifiable();
        var accessor = new DefaultBffTokenAccessor([user.Object, other.Object], CreateContexts());

        var result = await accessor.GetAccessTokenAsync(
            httpContext,
            request,
            CancellationToken.None
        );

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task GetAccessTokenAsync_UnknownName_Throws()
    {
        var user = CreateRequirement(BffTokenRequirementNames.User);
        var accessor = new DefaultBffTokenAccessor([user.Object], CreateContexts());
        var request = new BffTokenRequest(OtherName);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await accessor.GetAccessTokenAsync(
                CreateGuardedHttpContext(),
                request,
                CancellationToken.None
            )
        );

        Assert.Contains(OtherName, exception.Message);
    }

    [Fact]
    public async Task GetAccessTokenAsync_UnguardedEndpoint_Throws()
    {
        var user = CreateRequirement(BffTokenRequirementNames.User);
        var accessor = new DefaultBffTokenAccessor([user.Object], CreateContexts());
        var request = new BffTokenRequest(BffTokenRequirementNames.User);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await accessor.GetAccessTokenAsync(
                new DefaultHttpContext(),
                request,
                CancellationToken.None
            )
        );

        Assert.Contains("AsBffApi()", exception.Message);
    }

    [Fact]
    public async Task GetAccessTokenAsync_ContextOfUnguardedRequest_Throws()
    {
        var user = CreateRequirement(BffTokenRequirementNames.User);
        var accessor = new DefaultBffTokenAccessor([user.Object], CreateContexts());
        var context = CreateStrictMock<IBffTokenContext>();
        context.SetupGet(x => x.HttpContext).Returns(new DefaultHttpContext()).Verifiable();
        var request = new BffTokenRequest(BffTokenRequirementNames.User);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await accessor.GetAccessTokenAsync(context.Object, request, CancellationToken.None)
        );

        Assert.Contains("AsBffHub()", exception.Message);
    }

    [Fact]
    public async Task GetAccessTokenAsync_ContextWithoutHttpContext_Dispatches()
    {
        var user = CreateRequirement(BffTokenRequirementNames.User);
        var context = CreateStrictMock<IBffTokenContext>();
        context.SetupGet(x => x.HttpContext).Returns((HttpContext?)null).Verifiable();
        var request = new BffTokenRequest(BffTokenRequirementNames.User);
        var expected = BffTokenResult.Success(null);
        user.Setup(x => x.GetAccessTokenAsync(context.Object, request, CancellationToken.None))
            .Returns(new ValueTask<BffTokenResult>(expected))
            .Verifiable();
        var accessor = new DefaultBffTokenAccessor([user.Object], CreateContexts());

        var result = await accessor.GetAccessTokenAsync(
            context.Object,
            request,
            CancellationToken.None
        );

        Assert.Same(expected, result);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_DuplicateName_Throws()
    {
        var first = CreateRequirement(OtherName);
        var second = CreateRequirement(OtherName);

        Assert.Throws<InvalidOperationException>(() =>
            new DefaultBffTokenAccessor([first.Object, second.Object], CreateContexts())
        );
    }

    #endregion
}
