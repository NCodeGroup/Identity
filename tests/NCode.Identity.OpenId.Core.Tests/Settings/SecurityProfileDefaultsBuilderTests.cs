// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
