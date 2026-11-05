// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;

namespace NCode.Identity.Secrets.Persistence.Options;

public class OwnerRootKeyOptionsValidatorTests
{
    private static ValidateOptionsResult Validate(OwnerRootKeyOptions rootKeys) =>
        new OwnerRootKeyOptionsValidator().Validate(
            null,
            new SecretProtectionOptions { OwnerRootKeys = rootKeys }
        );

    [Fact]
    public void Validate_Defaults_Succeeds()
    {
        Assert.True(Validate(new OwnerRootKeyOptions()).Succeeded);
    }

    [Fact]
    public void Validate_DistinctProviders_Succeed()
    {
        var result = Validate(
            new OwnerRootKeyOptions { Providers = ["aws-kms", "azure-key-vault", "local"] }
        );

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_BoundaryIntervals_Succeed()
    {
        var rootKeys = new OwnerRootKeyOptions
        {
            ValidationInterval = TimeSpan.FromMinutes(1),
            RetryInterval = TimeSpan.FromMinutes(1),
        };
        Assert.True(Validate(rootKeys).Succeeded);

        rootKeys = new OwnerRootKeyOptions
        {
            ValidationInterval = TimeSpan.FromDays(1),
            RetryInterval = TimeSpan.FromSeconds(1),
        };
        Assert.True(Validate(rootKeys).Succeeded);
    }

    [Fact]
    public void Validate_DuplicateAndBlankProviders_Fail()
    {
        var result = Validate(new OwnerRootKeyOptions { Providers = ["aws-kms", "aws-kms", " "] });

        Assert.Equal(2, result.Failures?.Count());
        Assert.Contains(
            result.Failures ?? [],
            failure => failure.Contains("'aws-kms' more than once")
        );
        Assert.Contains(result.Failures ?? [], failure => failure.Contains("blank"));
    }

    [Theory]
    [InlineData(59)]
    [InlineData(86401)]
    public void Validate_ValidationIntervalOutOfRange_NamesTheKey(int seconds)
    {
        var result = Validate(
            new OwnerRootKeyOptions
            {
                ValidationInterval = TimeSpan.FromSeconds(seconds),
                RetryInterval = TimeSpan.FromSeconds(30),
            }
        );

        var failure = Assert.Single(result.Failures ?? []);
        Assert.StartsWith("SecretProtection:OwnerRootKeys:ValidationInterval ", failure);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3601)]
    public void Validate_RetryIntervalOutOfRange_NamesTheKey(int seconds)
    {
        var result = Validate(
            new OwnerRootKeyOptions
            {
                ValidationInterval = TimeSpan.FromHours(1),
                RetryInterval = TimeSpan.FromSeconds(seconds),
            }
        );

        var failure = Assert.Single(result.Failures ?? []);
        Assert.StartsWith("SecretProtection:OwnerRootKeys:RetryInterval ", failure);
    }

    [Fact]
    public void Validate_NullOptions_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new OwnerRootKeyOptionsValidator().Validate(null, null!)
        );
    }
}
