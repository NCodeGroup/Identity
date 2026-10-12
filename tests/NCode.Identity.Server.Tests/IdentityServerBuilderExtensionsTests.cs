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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NCode.Identity.Server.Frontend;
using Xunit;

namespace NCode.Identity.Server.Tests;

public sealed class IdentityServerBuilderExtensionsTests : BaseTests
{
    #region AddFrontendBootstrap Tests

    [Fact]
    public void AddFrontendBootstrap_BindsOptionsFromDefaultSection()
    {
        var options = ResolveOptions(
            new Dictionary<string, string?>
            {
                ["Frontend:ClientId"] = "spa",
                ["Frontend:Issuer"] = "https://issuer.example",
                ["Frontend:Features:dcr"] = "true",
                ["Frontend:Branding:DisplayName"] = "Acme",
            }
        );

        Assert.Equal("spa", options.ClientId);
        Assert.Equal("https://issuer.example", options.Issuer);
        Assert.True(options.Features["dcr"]);
        Assert.Equal("Acme", options.Branding?.DisplayName);
    }

    [Fact]
    public void AddFrontendBootstrap_BindsOptionsFromCustomSection()
    {
        var options = ResolveOptions(
            new Dictionary<string, string?> { ["Ui:ClientId"] = "custom-spa" },
            sectionName: "Ui"
        );

        Assert.Equal("custom-spa", options.ClientId);
    }

    [Fact]
    public void AddFrontendBootstrap_AppliesConfigureOverrideAfterBinding()
    {
        var options = ResolveOptions(
            new Dictionary<string, string?> { ["Frontend:ClientId"] = "from-config" },
            configureOptions: o => o.ClientId = "from-code"
        );

        Assert.Equal("from-code", options.ClientId);
    }

    private FrontendBootstrapOptions ResolveOptions(
        Dictionary<string, string?> configurationData,
        string? sectionName = null,
        Action<FrontendBootstrapOptions>? configureOptions = null
    )
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData)
            .Build();

        var serviceCollection = new ServiceCollection();
        var builder = CreateStrictMock<IIdentityServerBuilder>();
        builder.Setup(x => x.ServiceCollection).Returns(serviceCollection).Verifiable();

        var result = builder.Object.AddFrontendBootstrap(
            configuration,
            sectionName,
            configureOptions
        );
        Assert.Same(builder.Object, result);

        using var serviceProvider = serviceCollection.BuildServiceProvider();
        return serviceProvider.GetRequiredService<IOptions<FrontendBootstrapOptions>>().Value;
    }

    #endregion
}
