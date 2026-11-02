// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Xunit;

namespace NCode.Identity.Server.Tests.DevelopmentEnvironment;

public sealed class DeveloperKeysConfigurationTests
{
    private static IConfiguration CreateConfiguration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                values.Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value))
            )
            .Build();

    #region GetDeveloperKeysOptions Tests

    [Fact]
    public void GetDeveloperKeysOptions_WhenSectionIsAbsent_LeavesPruningOff()
    {
        var options = CreateConfiguration().GetDeveloperKeysOptions();

        Assert.False(options.PruneUnreadableKeys);
    }

    [Fact]
    public void GetDeveloperKeysOptions_WhenPruneUnreadableKeysIsTrue_BindsIt()
    {
        var options = CreateConfiguration(("DeveloperKeys:PruneUnreadableKeys", "true"))
            .GetDeveloperKeysOptions();

        Assert.True(options.PruneUnreadableKeys);
    }

    [Fact]
    public void GetDeveloperKeysOptions_WhenPruneUnreadableKeysIsNotABoolean_Throws()
    {
        var configuration = CreateConfiguration(("DeveloperKeys:PruneUnreadableKeys", "yes"));

        Assert.Throws<InvalidOperationException>(() => configuration.GetDeveloperKeysOptions());
    }

    [Fact]
    public void GetDeveloperKeysOptions_WhenSectionHasUnknownKey_Throws()
    {
        var configuration = CreateConfiguration(("DeveloperKeys:Prune", "true"));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            configuration.GetDeveloperKeysOptions()
        );

        Assert.Contains("'Prune'", exception.Message, StringComparison.Ordinal);
    }

    #endregion
}
