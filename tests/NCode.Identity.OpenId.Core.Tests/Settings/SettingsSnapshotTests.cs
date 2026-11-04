// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Settings;

public class SettingsSnapshotTests
{
    private static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web);

    private static SettingDescriptor<bool> FlagDescriptor { get; } =
        new() { Name = "flag", AppliesTo = ["platform"] };

    private static SettingDescriptor<List<string>> ListDescriptor { get; } =
        new() { Name = "list", AppliesTo = ["platform"] };

    private static SettingDescriptor<TimeSpan> LifetimeDescriptor { get; } =
        new() { Name = "lifetime", AppliesTo = ["platform"] };

    private static Setting<bool> CreateFlag(bool value) => new(FlagDescriptor, value);

    private static Setting<List<string>> CreateList(params string[] values) =>
        new(ListDescriptor, [.. values]);

    private static Setting<TimeSpan> CreateLifetime(TimeSpan value) =>
        new(LifetimeDescriptor, value);

    #region Write Tests

    [Fact]
    public void Write_WritesEachSettingByNameInOrdinalOrder()
    {
        var snapshot = SettingsSnapshot.Write(
            [CreateList("b", "a"), CreateFlag(true)],
            JsonOptions
        );

        Assert.Equal("""{"flag":true,"list":["b","a"]}""", snapshot.GetRawText());
    }

    [Fact]
    public void Write_WritesATimeSpanAsTheSerializerReadsIt()
    {
        var snapshot = SettingsSnapshot.Write(
            [CreateLifetime(TimeSpan.FromMinutes(5))],
            JsonOptions
        );

        Assert.Equal("""{"lifetime":"00:05:00"}""", snapshot.GetRawText());
    }

    [Fact]
    public void Write_WithNoSettings_WritesAnEmptyObject()
    {
        var snapshot = SettingsSnapshot.Write([], JsonOptions);

        Assert.Equal("{}", snapshot.GetRawText());
    }

    #endregion
}
