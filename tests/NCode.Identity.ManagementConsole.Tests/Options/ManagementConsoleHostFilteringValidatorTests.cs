// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.HostFiltering;
using NCode.Identity.AspNetCore;
using NCode.Identity.ManagementConsole.Options;
using Xunit;

namespace NCode.Identity.ManagementConsole.Tests.Options;

public class ManagementConsoleHostFilteringValidatorTests : BaseTests
{
    private static HostFilteringOptions CreateOptions(params string[] hosts)
    {
        var options = new HostFilteringOptions();
        foreach (var host in hosts)
        {
            options.AllowedHosts.Add(host);
        }

        return options;
    }

    private static ManagementConsoleHostFilteringValidator CreateValidator(
        bool localDevelopment = false
    ) => new(new LocalDevelopmentMode(localDevelopment));

    #region Validate Tests

    [Theory]
    [InlineData("*", "*")]
    [InlineData("a.example;*", "a.example", "*")]
    public void Validate_WhenTheSwitchIsOffAndAnyHostIsAllowed_Fails(
        string expectedValue,
        params string[] hosts
    )
    {
        var result = CreateValidator().Validate(null, CreateOptions(hosts));

        Assert.True(result.Failed);
        var failure = Assert.Single(result.Failures);
        Assert.Contains("AllowedHosts", failure);
        Assert.Contains("list this deployment's host names", failure);
        Assert.Contains("LocalDevelopment:Enabled", failure);
        Assert.Contains($"'{expectedValue}'", failure);
    }

    [Fact]
    public void Validate_WhenTheSwitchIsOffAndTheListIsEmpty_Fails()
    {
        var result = CreateValidator().Validate(null, CreateOptions());

        Assert.True(result.Failed);
        var failure = Assert.Single(result.Failures);
        Assert.Contains("AllowedHosts", failure);
        Assert.Contains("list this deployment's host names", failure);
        Assert.Contains("(value: '')", failure);
    }

    [Fact]
    public void Validate_WhenTheSwitchIsOffAndTheListNamesHosts_Succeeds()
    {
        var result = CreateValidator().Validate(null, CreateOptions("manage.example.com"));

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("*")]
    [InlineData("")]
    public void Validate_WhenTheSwitchIsOn_AllowsAnyHost(string host)
    {
        var options = host.Length == 0 ? CreateOptions() : CreateOptions(host);

        var result = CreateValidator(localDevelopment: true).Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WithNullOptions_Throws()
    {
        var validator = CreateValidator();

        var exception = Assert.Throws<ArgumentNullException>(() => validator.Validate(null, null!));

        Assert.Equal("options", exception.ParamName);
    }

    #endregion
}
