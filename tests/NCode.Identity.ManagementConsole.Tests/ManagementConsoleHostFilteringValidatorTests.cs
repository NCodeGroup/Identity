// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.HostFiltering;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace NCode.Identity.ManagementConsole.Tests;

public class ManagementConsoleHostFilteringValidatorTests : BaseTests
{
    private ManagementConsoleHostFilteringValidator CreateValidator(
        string environmentName = "Production"
    )
    {
        var mockEnvironment = CreateStrictMock<IHostEnvironment>();
        mockEnvironment.Setup(x => x.EnvironmentName).Returns(environmentName).Verifiable();
        return new ManagementConsoleHostFilteringValidator(mockEnvironment.Object);
    }

    private static HostFilteringOptions CreateOptions(params string[] hosts)
    {
        var options = new HostFilteringOptions();
        foreach (var host in hosts)
        {
            options.AllowedHosts.Add(host);
        }

        return options;
    }

    #region Validate Tests

    [Theory]
    [InlineData("*", "*")]
    [InlineData("a.example;*", "a.example", "*")]
    public void Validate_WhenProductionAllowsAnyHost_Fails(
        string expectedValue,
        params string[] hosts
    )
    {
        var result = CreateValidator().Validate(null, CreateOptions(hosts));

        Assert.True(result.Failed);
        var failure = Assert.Single(result.Failures);
        Assert.Contains("AllowedHosts", failure);
        Assert.Contains("list this deployment's host names", failure);
        Assert.Contains($"'{expectedValue}'", failure);
    }

    [Fact]
    public void Validate_WhenProductionListIsEmpty_Fails()
    {
        var result = CreateValidator().Validate(null, CreateOptions());

        Assert.True(result.Failed);
        var failure = Assert.Single(result.Failures);
        Assert.Contains("AllowedHosts", failure);
        Assert.Contains("list this deployment's host names", failure);
        Assert.Contains("(value: '')", failure);
    }

    [Fact]
    public void Validate_WhenProductionListsHostNames_Succeeds()
    {
        var result = CreateValidator().Validate(null, CreateOptions("manage.example.com"));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenDevelopmentAllowsAnyHost_Succeeds()
    {
        var result = CreateValidator("Development").Validate(null, CreateOptions("*"));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WithNullOptions_Throws()
    {
        var mockEnvironment = CreateStrictMock<IHostEnvironment>();
        var validator = new ManagementConsoleHostFilteringValidator(mockEnvironment.Object);

        var exception = Assert.Throws<ArgumentNullException>(() => validator.Validate(null, null!));

        Assert.Equal("options", exception.ParamName);
    }

    #endregion
}
