// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Registration;
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
