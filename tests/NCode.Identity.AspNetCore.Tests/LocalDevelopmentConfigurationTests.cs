// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Xunit;

namespace NCode.Identity.AspNetCore.Tests;

public sealed class LocalDevelopmentConfigurationTests
{
    private static IConfiguration CreateConfiguration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    #region IsLocalDevelopment Tests

    [Fact]
    public void IsLocalDevelopment_WhenSectionIsAbsent_ReturnsFalse()
    {
        var configuration = CreateConfiguration([]);

        Assert.False(configuration.IsLocalDevelopment());
    }

    [Fact]
    public void IsLocalDevelopment_WhenEnabledIsTrue_ReturnsTrue()
    {
        var configuration = CreateConfiguration(
            new Dictionary<string, string?> { ["LocalDevelopment:Enabled"] = "true" }
        );

        Assert.True(configuration.IsLocalDevelopment());
    }

    [Fact]
    public void IsLocalDevelopment_WhenEnabledIsFalse_ReturnsFalse()
    {
        var configuration = CreateConfiguration(
            new Dictionary<string, string?> { ["LocalDevelopment:Enabled"] = "false" }
        );

        Assert.False(configuration.IsLocalDevelopment());
    }

    [Fact]
    public void IsLocalDevelopment_WithAnUnknownKey_Throws()
    {
        var configuration = CreateConfiguration(
            new Dictionary<string, string?> { ["LocalDevelopment:Enabeld"] = "true" }
        );

        Assert.Throws<InvalidOperationException>(() => configuration.IsLocalDevelopment());
    }

    [Fact]
    public void IsLocalDevelopment_WithANullConfiguration_Throws()
    {
        IConfiguration configuration = null!;

        Assert.Throws<ArgumentNullException>(() => configuration.IsLocalDevelopment());
    }

    #endregion
}
