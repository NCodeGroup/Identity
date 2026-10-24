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

using System.Text.Json;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Servers;

public class ServerSettingsSnapshotTests
{
    private static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web);

    private static SettingDescriptor<bool> FlagDescriptor { get; } =
        new() { Name = "flag", AppliesTo = ["server"] };

    private static SettingDescriptor<List<string>> ListDescriptor { get; } =
        new() { Name = "list", AppliesTo = ["server"] };

    private static SettingDescriptor<TimeSpan> LifetimeDescriptor { get; } =
        new() { Name = "lifetime", AppliesTo = ["server"] };

    private static Setting<bool> CreateFlag(bool value) => new(FlagDescriptor, value);

    private static Setting<List<string>> CreateList(params string[] values) =>
        new(ListDescriptor, [.. values]);

    private static Setting<TimeSpan> CreateLifetime(TimeSpan value) =>
        new(LifetimeDescriptor, value);

    #region Write Tests

    [Fact]
    public void Write_WritesEachSettingByNameInOrdinalOrder()
    {
        var snapshot = ServerSettingsSnapshot.Write(
            [CreateList("b", "a"), CreateFlag(true)],
            JsonOptions
        );

        Assert.Equal("""{"flag":true,"list":["b","a"]}""", snapshot.GetRawText());
    }

    [Fact]
    public void Write_WritesATimeSpanAsTheSerializerReadsIt()
    {
        var snapshot = ServerSettingsSnapshot.Write(
            [CreateLifetime(TimeSpan.FromMinutes(5))],
            JsonOptions
        );

        Assert.Equal("""{"lifetime":"00:05:00"}""", snapshot.GetRawText());
    }

    [Fact]
    public void Write_WithNoSettings_WritesAnEmptyObject()
    {
        var snapshot = ServerSettingsSnapshot.Write([], JsonOptions);

        Assert.Equal("{}", snapshot.GetRawText());
    }

    #endregion
}
