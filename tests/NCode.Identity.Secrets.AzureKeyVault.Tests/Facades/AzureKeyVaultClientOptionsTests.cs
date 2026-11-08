// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Azure.Core;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using NCode.Identity.Secrets.AzureKeyVault.Facades;
using Xunit;

namespace NCode.Identity.Secrets.AzureKeyVault.Tests.Facades;

public class AzureKeyVaultClientOptionsTests
{
    #region Configure Tests

    [Fact]
    public void Configure_KeyClientOptions_SetsExponentialRetry()
    {
        var options = new KeyClientOptions();

        AzureKeyVaultClientOptions.Configure(options);

        Assert.Equal(RetryMode.Exponential, options.Retry.Mode);
        Assert.Equal(3, options.Retry.MaxRetries);
        Assert.Equal(TimeSpan.FromMilliseconds(500), options.Retry.Delay);
        Assert.Equal(TimeSpan.FromSeconds(8), options.Retry.MaxDelay);
    }

    [Fact]
    public void Configure_CryptographyClientOptions_SetsExponentialRetry()
    {
        var options = new CryptographyClientOptions();

        AzureKeyVaultClientOptions.Configure(options);

        Assert.Equal(RetryMode.Exponential, options.Retry.Mode);
        Assert.Equal(3, options.Retry.MaxRetries);
        Assert.Equal(TimeSpan.FromMilliseconds(500), options.Retry.Delay);
        Assert.Equal(TimeSpan.FromSeconds(8), options.Retry.MaxDelay);
    }

    [Fact]
    public void Configure_Options_ReturnsTheSameOptions()
    {
        var options = new KeyClientOptions();

        var result = AzureKeyVaultClientOptions.Configure(options);

        Assert.Same(options, result);
    }

    [Fact]
    public void Configure_NullOptions_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            AzureKeyVaultClientOptions.Configure<KeyClientOptions>(null!)
        );
    }

    #endregion

    #region ConfigureWithoutRetry Tests

    [Fact]
    public void ConfigureWithoutRetry_KeyClientOptions_SetsNoRetries()
    {
        var options = new KeyClientOptions();

        AzureKeyVaultClientOptions.ConfigureWithoutRetry(options);

        Assert.Equal(0, options.Retry.MaxRetries);
    }

    [Fact]
    public void ConfigureWithoutRetry_ConfiguredOptions_KeepsTheBackOff()
    {
        var options = AzureKeyVaultClientOptions.Configure(new KeyClientOptions());

        AzureKeyVaultClientOptions.ConfigureWithoutRetry(options);

        Assert.Equal(0, options.Retry.MaxRetries);
        Assert.Equal(RetryMode.Exponential, options.Retry.Mode);
        Assert.Equal(TimeSpan.FromMilliseconds(500), options.Retry.Delay);
        Assert.Equal(TimeSpan.FromSeconds(8), options.Retry.MaxDelay);
    }

    [Fact]
    public void ConfigureWithoutRetry_Options_ReturnsTheSameOptions()
    {
        var options = new KeyClientOptions();

        var result = AzureKeyVaultClientOptions.ConfigureWithoutRetry(options);

        Assert.Same(options, result);
    }

    [Fact]
    public void ConfigureWithoutRetry_NullOptions_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            AzureKeyVaultClientOptions.ConfigureWithoutRetry<KeyClientOptions>(null!)
        );
    }

    #endregion
}
