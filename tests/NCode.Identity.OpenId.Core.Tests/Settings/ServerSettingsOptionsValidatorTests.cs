// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using NCode.Identity.OpenId.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Settings;

public class ServerSettingsOptionsValidatorTests : BaseTests
{
    private static IConfigurationSection CreateSection() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["OpenId:Server:Settings:flag"] = "true" }
            )
            .Build()
            .GetSection("OpenId:Server:Settings");

    #region Validate Tests

    [Fact]
    public void Validate_WhenNoSettingsSection_Succeeds()
    {
        var validator = new ServerSettingsOptionsValidator(
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
            .Setup(x => x.Validate(section, OpenIdSettingScopes.Server))
            .Returns([])
            .Verifiable();
        var validator = new ServerSettingsOptionsValidator(mockReader.Object);
        var options = new OpenIdOptions { Server = { Settings = section } };

        var result = validator.Validate(name: null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenASettingCannotBeHonored_FailsWithEveryError()
    {
        var section = CreateSection();
        var mockReader = CreateStrictMock<ISettingConfigurationReader>();
        mockReader
            .Setup(x => x.Validate(section, OpenIdSettingScopes.Server))
            .Returns(["first error.", "second error."])
            .Verifiable();
        var validator = new ServerSettingsOptionsValidator(mockReader.Object);
        var options = new OpenIdOptions { Server = { Settings = section } };

        var result = validator.Validate(name: null, options);

        Assert.True(result.Failed);
        Assert.Equal(["first error.", "second error."], result.Failures);
    }

    #endregion
}
