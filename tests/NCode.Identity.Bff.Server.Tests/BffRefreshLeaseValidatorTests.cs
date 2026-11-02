// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Identity.Bff.Options;
using NCode.Identity.OpenId.Client.Options;
using Xunit;

namespace NCode.Identity.Bff.Server.Tests;

public class BffRefreshLeaseValidatorTests : BaseTests
{
    private BffRefreshLeaseValidator CreateValidator(TimeSpan totalTimeout)
    {
        var mockClientOptions = CreateStrictMock<IOptions<OpenIdClientOptions>>();
        mockClientOptions
            .Setup(x => x.Value)
            .Returns(new OpenIdClientOptions { TotalTimeout = totalTimeout })
            .Verifiable();
        return new BffRefreshLeaseValidator(mockClientOptions.Object);
    }

    private static BffOptions CreateOptions(TimeSpan refreshLeaseDuration) =>
        new() { Tokens = new BffTokenOptions { RefreshLeaseDuration = refreshLeaseDuration } };

    #region Validate Tests

    [Fact]
    public void Validate_DefaultLeaseAndTimeout_Succeeds()
    {
        var validator = CreateValidator(TimeSpan.FromSeconds(15));

        var result = validator.Validate(null, CreateOptions(TimeSpan.FromSeconds(40)));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_LeaseShorterThanTwiceTimeoutPlusMargin_Fails()
    {
        var validator = CreateValidator(TimeSpan.FromSeconds(15));

        var result = validator.Validate(null, CreateOptions(TimeSpan.FromSeconds(30)));

        Assert.True(result.Failed);
        Assert.Contains(result.Failures ?? [], x => x.Contains("Bff:Tokens:RefreshLeaseDuration"));
    }

    [Fact]
    public void Validate_LeaseEqualToMinimum_Succeeds()
    {
        var validator = CreateValidator(TimeSpan.FromSeconds(60));

        var result = validator.Validate(null, CreateOptions(TimeSpan.FromSeconds(130)));

        Assert.True(result.Succeeded);
    }

    #endregion
}
