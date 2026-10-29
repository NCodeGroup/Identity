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

using Microsoft.Extensions.Options;
using NCode.Identity.Bff.Options;
using NCode.Identity.OpenId.Client.Options;
using Xunit;

namespace NCode.Identity.Bff.Server.Tests;

public class BffRefreshLeaseValidatorTests : BaseTests
{
    private BffRefreshLeaseValidator CreateValidator(TimeSpan requestTimeout)
    {
        var mockClientOptions = CreateStrictMock<IOptions<OpenIdClientOptions>>();
        mockClientOptions
            .Setup(x => x.Value)
            .Returns(new OpenIdClientOptions { RequestTimeout = requestTimeout })
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
