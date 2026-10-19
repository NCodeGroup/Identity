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
    public void GetDeveloperKeysOptions_WhenSectionIsAbsent_LeavesTheModeUnset()
    {
        var options = CreateConfiguration().GetDeveloperKeysOptions();

        Assert.Null(options.Mode);
    }

    [Theory]
    [InlineData("Ephemeral", DeveloperKeyMode.Ephemeral)]
    [InlineData("persistentsigningkey", DeveloperKeyMode.PersistentSigningKey)]
    [InlineData("None", DeveloperKeyMode.None)]
    public void GetDeveloperKeysOptions_WhenModeIsConfigured_BindsIt(
        string value,
        DeveloperKeyMode expected
    )
    {
        var options = CreateConfiguration(("DeveloperKeys:Mode", value)).GetDeveloperKeysOptions();

        Assert.Equal(expected, options.Mode);
    }

    [Fact]
    public void GetDeveloperKeysOptions_WhenModeIsMisspelled_Throws()
    {
        // A lenient read would fall back to Ephemeral and silently regenerate the signing key on every restart.
        var configuration = CreateConfiguration(("DeveloperKeys:Mode", "Persistent"));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            configuration.GetDeveloperKeysOptions()
        );

        Assert.Contains("'DeveloperKeys'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetDeveloperKeysOptions_WhenModeIsAnUndefinedNumber_Throws()
    {
        var configuration = CreateConfiguration(("DeveloperKeys:Mode", "5"));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            configuration.GetDeveloperKeysOptions()
        );

        Assert.Contains("'DeveloperKeys:Mode'", exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(DeveloperKeyMode.PersistentSigningKey), exception.Message);
    }

    [Fact]
    public void GetDeveloperKeysOptions_WhenSectionHasUnknownKey_Throws()
    {
        var configuration = CreateConfiguration(("DeveloperKeys:Mdoe", "None"));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            configuration.GetDeveloperKeysOptions()
        );

        Assert.Contains("'Mdoe'", exception.Message, StringComparison.Ordinal);
    }

    #endregion
}
