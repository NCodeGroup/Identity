// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Hosting;
using NCode.Identity.AspNetCore.Options;
using Xunit;

namespace NCode.Identity.AspNetCore.Tests.Options;

public sealed class LocalDevelopmentOptionsValidatorTests : BaseTests
{
    private LocalDevelopmentOptionsValidator CreateValidator(string environmentName)
    {
        var mockEnvironment = CreateStrictMock<IHostEnvironment>();
        mockEnvironment.Setup(x => x.EnvironmentName).Returns(environmentName).Verifiable();

        return new LocalDevelopmentOptionsValidator(mockEnvironment.Object);
    }

    #region Validate Tests

    [Fact]
    public void Validate_WhenEnabledInProduction_Fails()
    {
        var validator = CreateValidator(Environments.Production);

        var result = validator.Validate(name: null, new LocalDevelopmentOptions { Enabled = true });

        Assert.True(result.Failed);
        Assert.Contains(
            "LocalDevelopment:Enabled is a local inner-loop switch and the Production environment refuses it",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    public void Validate_WhenEnabledOutsideProduction_Succeeds(string environmentName)
    {
        var validator = CreateValidator(environmentName);

        var result = validator.Validate(name: null, new LocalDevelopmentOptions { Enabled = true });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenNotEnabledInProduction_Succeeds()
    {
        var mockEnvironment = CreateStrictMock<IHostEnvironment>();
        var validator = new LocalDevelopmentOptionsValidator(mockEnvironment.Object);

        var result = validator.Validate(
            name: null,
            new LocalDevelopmentOptions { Enabled = false }
        );

        Assert.True(result.Succeeded);
    }

    #endregion
}
