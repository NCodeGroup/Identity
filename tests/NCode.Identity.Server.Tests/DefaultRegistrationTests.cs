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

using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.Server.Tests;

public sealed class DefaultRegistrationTests
{
    private static IHost BuildHost(Dictionary<string, string?> configurationData)
    {
        var hostBuilder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        hostBuilder.Configuration.AddInMemoryCollection(configurationData);
        hostBuilder.Services.AddIdentityServer().AddConfiguration(hostBuilder.Configuration);

        // These tests exercise configuration only; the persistence provider a host composes is never called.
        hostBuilder.Services.AddSingleton(
            new Mock<IStoreManagerFactory>(MockBehavior.Strict).Object
        );

        return hostBuilder.Build();
    }

    public static TheoryData<string> SettingNames { get; } =
        new(
            typeof(OpenIdSettingNames)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.IsLiteral)
                .Select(field => field.GetRawConstantValue())
                .OfType<string>()
        );

    #region AddIdentityServer Tests

    [Theory]
    [MemberData(nameof(SettingNames))]
    public void AddIdentityServer_RegistersADescriptorForEverySettingName(string settingName)
    {
        // Setting names are a closed vocabulary: a name without a descriptor cannot be configured, stored, or loaded.
        using var host = BuildHost([]);
        var descriptors = host.Services.GetRequiredService<ISettingDescriptorCollectionProvider>();

        Assert.True(
            descriptors.Collection.TryGet(settingName, out _),
            $"The setting '{settingName}' has no registered descriptor."
        );
    }

    [Fact]
    public void AddIdentityServer_WhenServerSettingsCanBeHonored_BindsThem()
    {
        using var host = BuildHost(
            new Dictionary<string, string?>
            {
                ["OpenId:Server:Settings:grant_types_supported:0"] = "authorization_code",
                ["OpenId:Server:Settings:session_management_supported"] = "true",
                ["OpenId:Server:Settings:acr_values_supported"] = null,
            }
        );

        var options = host.Services.GetRequiredService<IOptions<OpenIdOptions>>().Value;

        Assert.Equal("OpenId:Server:Settings", options.Server.Settings?.Path);
    }

    [Theory]
    [InlineData("tenant_issuer", "https://op.example")]
    [InlineData("session_management_supported", "yes")]
    [InlineData("grant_types_supported", "authorization_code")]
    public async Task AddIdentityServer_WhenServerSettingCannotBeHonored_FailsToStart(
        string name,
        string value
    )
    {
        using var host = BuildHost(
            new Dictionary<string, string?> { [$"OpenId:Server:Settings:{name}"] = value }
        );

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() =>
            host.StartAsync()
        );

        Assert.Contains(
            $"OpenId:Server:Settings:{name}",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    #endregion
}
