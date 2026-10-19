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
