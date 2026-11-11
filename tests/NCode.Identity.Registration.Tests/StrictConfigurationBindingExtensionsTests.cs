// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Xunit;

namespace NCode.Identity.Registration.Tests;

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
