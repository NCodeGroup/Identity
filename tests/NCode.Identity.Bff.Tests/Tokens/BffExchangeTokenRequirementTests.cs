// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Moq;
using NCode.Identity.Bff.Tokens;
using Xunit;

namespace NCode.Identity.Bff.Tests.Tokens;

public class BffExchangeTokenRequirementTests : BaseTests
{
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
        var expected = BffTokenResult.Success(
            new BffAccessToken("exchanged", "Bearer", DateTimeOffset.UnixEpoch)
        );
        MockService
            .Setup(x =>
                x.GetAccessTokenAsync(
                    httpContext,
                    "https://api.example",
                    "read",
                    CancellationToken.None
                )
            )
            .ReturnsAsync(expected)
            .Verifiable();

        var result = await Requirement.GetAccessTokenAsync(
            httpContext,
            new BffTokenRequest(BffTokenRequirementNames.Exchange)
            {
                Resource = "https://api.example",
                Scope = "read",
            },
            CancellationToken.None
        );

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task GetAccessTokenAsync_NullRequest_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            Requirement
                .GetAccessTokenAsync(new DefaultHttpContext(), null!, CancellationToken.None)
                .AsTask()
        );
    }

    #endregion
}
