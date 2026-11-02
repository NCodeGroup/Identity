// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace NCode.Identity.Bff.Tests;

public class BffTokenResultTests : BaseTests
{
    #region BffTokenRequest Tests

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void BffTokenRequest_EmptyRequirement_Throws(string? requirement)
    {
        Assert.ThrowsAny<ArgumentException>(() => new BffTokenRequest(requirement!));
    }

    #endregion

    #region BffAccessToken Tests

    [Theory]
    [InlineData(null, "Bearer")]
    [InlineData("", "Bearer")]
    [InlineData("token", null)]
    [InlineData("token", "")]
    public void BffAccessToken_EmptyArgument_Throws(string? value, string? tokenType)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            new BffAccessToken(value!, tokenType!, DateTimeOffset.UnixEpoch)
        );
    }

    #endregion

    #region Success Tests

    [Fact]
    public void Success_NullToken_IsSuccessWithoutToken()
    {
        var result = BffTokenResult.Success(null);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Token);
        Assert.Null(result.Reason);
        Assert.Equal(200, result.StatusCode);
    }

    [Fact]
    public void Success_Token_EchoesToken()
    {
        var token = new BffAccessToken("token", "Bearer", DateTimeOffset.UnixEpoch);

        var result = BffTokenResult.Success(token);

        Assert.True(result.IsSuccess);
        Assert.Same(token, result.Token);
    }

    #endregion

    #region Failure Tests

    [Fact]
    public void Failure_ValidArguments_EchoesStatusAndReason()
    {
        var result = BffTokenResult.Failure(401, BffTokenFailureReasons.RefreshFailed);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Token);
        Assert.Equal(401, result.StatusCode);
        Assert.Equal(BffTokenFailureReasons.RefreshFailed, result.Reason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Failure_EmptyReason_Throws(string? reason)
    {
        Assert.ThrowsAny<ArgumentException>(() => BffTokenResult.Failure(401, reason!));
    }

    [Fact]
    public void Failure_SuccessStatusCode_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BffTokenResult.Failure(200, BffTokenFailureReasons.ProviderError)
        );
    }

    #endregion
}
