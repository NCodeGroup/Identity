// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.Secrets.Persistence.Options;

public class RemoteKeyOptionsValidatorTests : BaseTests
{
    private ValidateOptionsResult Validate(RemoteKeyOptions keys, int providerCount = 0)
    {
        var catalog = CreateStrictMock<IRemoteKeyDescriptorCatalog>();
        catalog
            .SetupGet(x => x.Providers)
            .Returns(
                Enumerable
                    .Range(0, providerCount)
                    .Select(index => new RemoteKeyProviderDescriptor { Name = $"provider-{index}" })
                    .ToList()
            )
            .Verifiable();

        return new RemoteKeyOptionsValidator(catalog.Object).Validate(
            null,
            new SecretProtectionOptions { RemoteKeys = keys }
        );
    }

    #region Validate Tests

    [Fact]
    public void Validate_DefaultsWithoutProviders_Succeeds()
    {
        Assert.True(Validate(new RemoteKeyOptions()).Succeeded);
    }

    [Fact]
    public void Validate_BoundaryValues_Succeed()
    {
        var keys = new RemoteKeyOptions
        {
            Deployment = new string('a', 40),
            SweepInterval = TimeSpan.FromMinutes(1),
            SweepGracePeriod = TimeSpan.FromHours(1),
        };

        Assert.True(Validate(keys, providerCount: 1).Succeeded);
    }

    [Fact]
    public void Validate_ValidDeploymentWithProviders_Succeeds()
    {
        var keys = new RemoteKeyOptions { Deployment = "prod-eu-1" };

        Assert.True(Validate(keys, providerCount: 2).Succeeded);
    }

    [Fact]
    public void Validate_MissingDeploymentWithProviders_NamesTheKey()
    {
        var result = Validate(new RemoteKeyOptions(), providerCount: 1);

        var failure = Assert.Single(result.Failures ?? []);
        Assert.Equal(
            "SecretProtection:RemoteKeys:Deployment is required because a remote key provider is registered.",
            failure
        );
    }

    [Fact]
    public void Validate_EmptyDeploymentWithProviders_NamesTheKey()
    {
        var result = Validate(new RemoteKeyOptions { Deployment = "" }, providerCount: 1);

        var failure = Assert.Single(result.Failures ?? []);
        Assert.StartsWith("SecretProtection:RemoteKeys:Deployment ", failure);
    }

    [Theory]
    [InlineData("Prod")]
    [InlineData("prod_eu")]
    [InlineData("-prod")]
    [InlineData("prod-")]
    [InlineData("prod--eu")]
    [InlineData("prod eu")]
    [InlineData("prod\n")]
    public void Validate_DeploymentWithInvalidShape_NamesTheKey(string deployment)
    {
        var result = Validate(new RemoteKeyOptions { Deployment = deployment });

        var failure = Assert.Single(result.Failures ?? []);
        Assert.StartsWith("SecretProtection:RemoteKeys:Deployment ", failure);
    }

    [Fact]
    public void Validate_DeploymentLongerThanTheMaximum_NamesTheKey()
    {
        var result = Validate(new RemoteKeyOptions { Deployment = new string('a', 41) });

        var failure = Assert.Single(result.Failures ?? []);
        Assert.StartsWith("SecretProtection:RemoteKeys:Deployment ", failure);
    }

    [Fact]
    public void Validate_SweepIntervalBelowTheMinimum_NamesTheKey()
    {
        var result = Validate(new RemoteKeyOptions { SweepInterval = TimeSpan.FromSeconds(59) });

        var failure = Assert.Single(result.Failures ?? []);
        Assert.StartsWith("SecretProtection:RemoteKeys:SweepInterval ", failure);
    }

    [Fact]
    public void Validate_SweepIntervalAboveTheTimerMaximum_NamesTheKey()
    {
        var result = Validate(new RemoteKeyOptions { SweepInterval = TimeSpan.FromDays(60) });

        var failure = Assert.Single(result.Failures ?? []);
        Assert.StartsWith("SecretProtection:RemoteKeys:SweepInterval ", failure);
    }

    [Fact]
    public void Validate_SweepGracePeriodBelowTheMinimum_NamesTheKey()
    {
        var result = Validate(new RemoteKeyOptions { SweepGracePeriod = TimeSpan.FromMinutes(59) });

        var failure = Assert.Single(result.Failures ?? []);
        Assert.StartsWith("SecretProtection:RemoteKeys:SweepGracePeriod ", failure);
    }

    [Fact]
    public void Validate_SeveralProblems_ReportsEach()
    {
        var result = Validate(
            new RemoteKeyOptions
            {
                Deployment = "Bad",
                SweepInterval = TimeSpan.Zero,
                SweepGracePeriod = TimeSpan.Zero,
            }
        );

        Assert.Equal(3, (result.Failures ?? []).Count());
    }

    #endregion
}
