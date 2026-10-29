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

    #region GetAccessTokenAsync Tests

    [Fact]
    public async Task GetAccessTokenAsync_KnownName_DispatchesToRequirement()
    {
        var user = CreateRequirement(BffTokenRequirementNames.User);
        var other = CreateRequirement(OtherName);
        var httpContext = new DefaultHttpContext();
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
                new DefaultHttpContext(),
                request,
                CancellationToken.None
            )
        );

        Assert.Contains(OtherName, exception.Message);
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
