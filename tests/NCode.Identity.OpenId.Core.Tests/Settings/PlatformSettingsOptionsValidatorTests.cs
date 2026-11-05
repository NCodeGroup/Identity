// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using NCode.Identity.OpenId.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Settings;

public class PlatformSettingsOptionsValidatorTests : BaseTests
{
    private static IConfigurationSection CreateSection() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["OpenId:Platform:Settings:flag"] = "true" }
            )
            .Build()
            .GetSection("OpenId:Platform:Settings");

    #region Validate Tests

    [Fact]
    public void Validate_WhenNoSettingsSection_Succeeds()
    {
        var validator = new PlatformSettingsOptionsValidator(
            CreateStrictMock<ISettingConfigurationReader>().Object
        );

        var result = validator.Validate(name: null, new OpenIdOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenEverySettingCanBeHonored_Succeeds()
    {
        var section = CreateSection();
        var mockReader = CreateStrictMock<ISettingConfigurationReader>();
        mockReader
            .Setup(x => x.Validate(section, OpenIdSettingScopes.Platform))
            .Returns([])
            .Verifiable();
        var validator = new PlatformSettingsOptionsValidator(mockReader.Object);
        var options = new OpenIdOptions { Platform = { Settings = section } };

        var result = validator.Validate(name: null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenASettingCannotBeHonored_FailsWithEveryError()
    {
        var section = CreateSection();
        var mockReader = CreateStrictMock<ISettingConfigurationReader>();
        mockReader
            .Setup(x => x.Validate(section, OpenIdSettingScopes.Platform))
            .Returns(["first error.", "second error."])
            .Verifiable();
        var validator = new PlatformSettingsOptionsValidator(mockReader.Object);
        var options = new OpenIdOptions { Platform = { Settings = section } };

        var result = validator.Validate(name: null, options);

        Assert.True(result.Failed);
        Assert.Equal(["first error.", "second error."], result.Failures);
    }

    #endregion
}
