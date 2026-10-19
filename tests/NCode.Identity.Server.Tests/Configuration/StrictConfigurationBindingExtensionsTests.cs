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
using NCode.Identity.Server.Configuration;
using Xunit;

namespace NCode.Identity.Server.Tests.Configuration;

public sealed class StrictConfigurationBindingExtensionsTests
{
    #region BindStrictly Tests

    [Fact]
    public void BindStrictly_WhenEveryKeyMaps_BindsTheValues()
    {
        var section = CreateSection(
            new Dictionary<string, string?>
            {
                ["Sample:Name"] = "acme",
                ["Sample:Nested:Count"] = "3",
            }
        );
        var instance = new SampleOptions();

        section.BindStrictly(instance);

        Assert.Equal("acme", instance.Name);
        Assert.Equal(3, instance.Nested.Count);
    }

    [Fact]
    public void BindStrictly_WhenSectionIsAbsent_LeavesDefaults()
    {
        var section = CreateSection([]);
        var instance = new SampleOptions();

        section.BindStrictly(instance);

        Assert.Equal("default", instance.Name);
        Assert.Equal(0, instance.Nested.Count);
    }

    [Fact]
    public void BindStrictly_WhenKeyMapsToNothing_ThrowsNamingTheSectionAndKey()
    {
        var section = CreateSection(new Dictionary<string, string?> { ["Sample:Nmae"] = "acme" });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            section.BindStrictly(new SampleOptions())
        );

        Assert.Contains("'Sample'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("'Nmae'", exception.Message, StringComparison.Ordinal);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    [Fact]
    public void BindStrictly_WhenNestedKeyMapsToNothing_Throws()
    {
        var section = CreateSection(
            new Dictionary<string, string?> { ["Sample:Nested:Total"] = "3" }
        );

        var exception = Assert.Throws<InvalidOperationException>(() =>
            section.BindStrictly(new SampleOptions())
        );

        Assert.Contains("'Total'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BindStrictly_WhenValueCannotBeConverted_ThrowsNamingTheSection()
    {
        var section = CreateSection(
            new Dictionary<string, string?> { ["Sample:Nested:Count"] = "many" }
        );

        var exception = Assert.Throws<InvalidOperationException>(() =>
            section.BindStrictly(new SampleOptions())
        );

        Assert.Contains("'Sample'", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    private static IConfigurationSection CreateSection(Dictionary<string, string?> data)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
        return configuration.GetSection("Sample");
    }

    private sealed class SampleOptions
    {
        public string Name { get; set; } = "default";

        public NestedOptions Nested { get; set; } = new();
    }

    private sealed class NestedOptions
    {
        public int Count { get; set; }
    }
}
