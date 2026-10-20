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
using NCode.Registration;
using Xunit;

namespace NCode.Identity.Server.Tests;

public sealed class DeveloperInteractiveSeedRegistrationTests : BaseTests
{
    private IServiceBuilder<IdentityServer> CreateBuilder()
    {
        var builder = CreateStrictMock<IServiceBuilder<IdentityServer>>();
        builder.Setup(x => x.ServiceCollection).Returns(new ServiceCollection()).Verifiable();
        return builder.Object;
    }

    private static IConfiguration CreateConfiguration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    #region AddDeveloperInteractiveSeed Tests

    [Fact]
    public void AddDeveloperInteractiveSeed_WhenNothingToSeed_NeedsNoTenant()
    {
        var builder = CreateBuilder();

        var result = builder.AddDeveloperInteractiveSeed(CreateConfiguration([]));

        Assert.Same(builder, result);
    }

    [Fact]
    public void AddDeveloperInteractiveSeed_WhenSomethingToSeedAndATenantIsNamed_Registers()
    {
        var builder = CreateBuilder();

        var result = builder.AddDeveloperInteractiveSeed(
            CreateConfiguration(
                new Dictionary<string, string?>
                {
                    ["DeveloperInteractiveSeed:TenantId"] = "default",
                    ["DeveloperInteractiveSeed:AdminPassword"] = "dev-password",
                }
            )
        );

        Assert.Same(builder, result);
    }

    [Fact]
    public void AddDeveloperInteractiveSeed_WhenSomethingToSeedAndNoTenantIsNamed_Throws()
    {
        var builder = CreateBuilder();

        // Seeding every tenant would hand each tenant created at runtime a well-known administrator.
        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.AddDeveloperInteractiveSeed(
                CreateConfiguration(
                    new Dictionary<string, string?>
                    {
                        ["DeveloperInteractiveSeed:AdminPassword"] = "dev-password",
                    }
                )
            )
        );

        Assert.Contains(
            "DeveloperInteractiveSeed:TenantId is required",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    #endregion
}
