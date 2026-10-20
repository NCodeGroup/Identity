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

using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Settings;

public class SecurityProfileDefaultsBuilderTests : BaseTests
{
    private const string SettingName = "signed_response_alg";

    private static SettingKey<string> Key { get; } = new(SettingName);

    private SecurityProfileDefaultsBuilder CreateBuilder(Func<string, string, string> merge)
    {
        var descriptor = new SettingDescriptor<string>
        {
            Name = SettingName,
            AppliesTo = ["client"],
            OnMerge = merge,
        };
        Setting<string>? setting = descriptor.Create("PS256");

        var mockDefaults = CreateStrictMock<ISettingCollection>();
        mockDefaults.Setup(x => x.Set(Key, "PS256")).Verifiable();
        mockDefaults.Setup(x => x.TryGet(Key, out setting)).Returns(true).Verifiable();

        return new SecurityProfileDefaultsBuilder("custom", mockDefaults.Object);
    }

    #region Set Tests

    [Fact]
    public void Set_WhenTheSettingMergesWithAProjection_SetsTheDefault()
    {
        var builder = CreateBuilder(SettingMerge.Replace);

        var exception = Record.Exception(() => builder.Set(Key, "PS256"));

        Assert.Null(exception);
    }

    [Fact]
    public void Set_WhenTheSettingMergesWithALatticeOperation_ThrowsNamingTheProfileAndTheSetting()
    {
        var builder = CreateBuilder(SettingMerge.Min);

        var exception = Assert.Throws<InvalidOperationException>(() => builder.Set(Key, "PS256"));

        Assert.Contains("'custom'", exception.Message, StringComparison.Ordinal);
        Assert.Contains($"'{SettingName}'", exception.Message, StringComparison.Ordinal);
    }

    #endregion
}
