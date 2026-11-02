// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
        var descriptor = new SettingDescriptor<bool>
        {
            Name = SettingName,
            AppliesTo = ["client"],
            OnMerge = merge,
        };
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
