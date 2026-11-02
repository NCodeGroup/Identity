// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Moq;
using NCode.Identity.Bff.Tokens;
using Xunit;

namespace NCode.Identity.Bff.Tests.Tokens;

public class DefaultBffTokenAccessorTests : BaseTests
{
    private const string OtherName = "other";

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
            .Setup(x => x.GetAccessTokenAsync(httpContext, request, CancellationToken.None))
            .Returns(new ValueTask<BffTokenResult>(expected))
            .Verifiable();
        var accessor = new DefaultBffTokenAccessor([user.Object, other.Object]);

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
        var accessor = new DefaultBffTokenAccessor([user.Object]);
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
        var accessor = new DefaultBffTokenAccessor([user.Object]);
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

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_DuplicateName_Throws()
    {
        var first = CreateRequirement(OtherName);
        var second = CreateRequirement(OtherName);

        Assert.Throws<InvalidOperationException>(() =>
            new DefaultBffTokenAccessor([first.Object, second.Object])
        );
    }

    #endregion
}
