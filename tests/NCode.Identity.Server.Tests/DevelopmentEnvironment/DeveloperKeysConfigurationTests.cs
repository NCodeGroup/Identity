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
