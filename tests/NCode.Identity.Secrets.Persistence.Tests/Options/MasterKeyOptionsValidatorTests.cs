// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;

namespace NCode.Identity.Secrets.Persistence.Options;

public class MasterKeyOptionsValidatorTests
{
    private static ValidateOptionsResult Validate(MasterKeyOptions keys) =>
        new MasterKeyOptionsValidator().Validate(
            null,
            new SecretProtectionOptions { MasterKeys = keys }
        );

    #region Validate Tests

    [Fact]
    public void Validate_Defaults_Succeeds()
    {
        Assert.True(Validate(new MasterKeyOptions()).Succeeded);
    }

    [Fact]
    public void Validate_BoundaryValues_Succeed()
    {
        var keys = new MasterKeyOptions
        {
            RotationPeriod = TimeSpan.FromDays(1),
            RefreshInterval = TimeSpan.FromSeconds(1),
            RetirementGrace = TimeSpan.FromSeconds(2),
            MaintenanceInterval = TimeSpan.FromMinutes(1),
        };

        Assert.True(Validate(keys).Succeeded);
    }

    [Fact]
    public void Validate_ShortRotationPeriod_NamesTheKey()
    {
        var result = Validate(new MasterKeyOptions { RotationPeriod = TimeSpan.FromHours(23) });

        var failure = Assert.Single(result.Failures ?? []);
        Assert.StartsWith("SecretProtection:MasterKeys:RotationPeriod ", failure);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3601)]
    public void Validate_RefreshIntervalOutOfRange_NamesTheKey(int seconds)
    {
        var result = Validate(
            new MasterKeyOptions
            {
                RefreshInterval = TimeSpan.FromSeconds(seconds),
                RetirementGrace = TimeSpan.FromDays(30),
            }
        );

        var failure = Assert.Single(result.Failures ?? []);
        Assert.StartsWith("SecretProtection:MasterKeys:RefreshInterval ", failure);
    }

    [Fact]
    public void Validate_GraceBelowTwiceRefresh_NamesTheKey()
    {
        var result = Validate(
            new MasterKeyOptions
            {
                RefreshInterval = TimeSpan.FromMinutes(5),
                RetirementGrace = TimeSpan.FromMinutes(9),
            }
        );

        var failure = Assert.Single(result.Failures ?? []);
        Assert.StartsWith("SecretProtection:MasterKeys:RetirementGrace ", failure);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60 * 60 * 24 * 60)]
    public void Validate_MaintenanceIntervalOutOfRange_NamesTheKey(int seconds)
    {
        var result = Validate(
            new MasterKeyOptions { MaintenanceInterval = TimeSpan.FromSeconds(seconds) }
        );

        var failure = Assert.Single(result.Failures ?? []);
        Assert.StartsWith("SecretProtection:MasterKeys:MaintenanceInterval ", failure);
    }

    [Fact]
    public void Validate_SeveralProblems_ReportsEach()
    {
        var result = Validate(
            new MasterKeyOptions
            {
                RotationPeriod = TimeSpan.Zero,
                MaintenanceInterval = TimeSpan.Zero,
            }
        );

        Assert.Equal(2, (result.Failures ?? []).Count());
    }

    #endregion
}
