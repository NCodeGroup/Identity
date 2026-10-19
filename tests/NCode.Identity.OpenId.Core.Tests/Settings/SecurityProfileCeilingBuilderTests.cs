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

public class SecurityProfileCeilingBuilderTests : BaseTests
{
    private const string SettingName = "require_it";

    private static SettingKey<bool> Key { get; } = new(SettingName);

    private SecurityProfileCeilingBuilder CreateBuilder(Func<bool, bool, bool> merge)
    {
        var descriptor = new SettingDescriptor<bool> { Name = SettingName, OnMerge = merge };
        Setting<bool>? setting = descriptor.Create(true);

        var mockCeiling = CreateStrictMock<ISettingCollection>();
        mockCeiling.Setup(x => x.Set(Key, true)).Verifiable();
        mockCeiling.Setup(x => x.TryGet(Key, out setting)).Returns(true).Verifiable();

        return new SecurityProfileCeilingBuilder("custom", mockCeiling.Object);
    }

    #region Set Tests

    [Fact]
    public void Set_WhenTheSettingMergesWithALatticeOperation_SetsTheCeiling()
    {
        var builder = CreateBuilder(SettingMerge.Or);

        var exception = Record.Exception(() => builder.Set(Key, true));

        Assert.Null(exception);
    }

    [Fact]
    public void Set_WhenTheSettingMergesWithAProjection_ThrowsNamingTheProfileAndTheSetting()
    {
        var builder = CreateBuilder(SettingMerge.Replace);

        var exception = Assert.Throws<InvalidOperationException>(() => builder.Set(Key, true));

        Assert.Contains("'custom'", exception.Message, StringComparison.Ordinal);
        Assert.Contains($"'{SettingName}'", exception.Message, StringComparison.Ordinal);
    }

    #endregion
}
