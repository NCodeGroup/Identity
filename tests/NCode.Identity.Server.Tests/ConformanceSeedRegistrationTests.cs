// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NCode.Registration;
using Xunit;

namespace NCode.Identity.Server.Tests;

public sealed class ConformanceSeedRegistrationTests : BaseTests
{
    private static IConfiguration CreateConfiguration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    #region AddConformanceSeed Tests

    [Fact]
    public void AddConformanceSeed_WhenSomethingToSeedAndATenantIsNamed_Registers()
    {
        var builder = CreateStrictMock<IServiceBuilder<IdentityServer>>();
        builder.Setup(x => x.ServiceCollection).Returns(new ServiceCollection()).Verifiable();

        var result = builder.Object.AddConformanceSeed(
            CreateConfiguration(
                new Dictionary<string, string?>
                {
                    ["ConformanceSeed:TenantId"] = "conformance",
                    ["ConformanceSeed:TestUserPassword"] = "conformance-password",
                }
            )
        );

        Assert.Same(builder.Object, result);
    }

    [Fact]
    public void AddConformanceSeed_WhenSomethingToSeedAndNoTenantIsNamed_Throws()
    {
        var mockBuilder = CreateStrictMock<IServiceBuilder<IdentityServer>>();
        mockBuilder.Setup(x => x.ServiceCollection).Returns(new ServiceCollection()).Verifiable();
        var builder = mockBuilder.Object;

        // Seeding every tenant would hand each tenant created at runtime the suite's well-known credentials.
        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.AddConformanceSeed(
                CreateConfiguration(
                    new Dictionary<string, string?>
                    {
                        ["ConformanceSeed:TestUserPassword"] = "conformance-password",
                    }
                )
            )
        );

        Assert.Contains(
            "ConformanceSeed:TenantId is required",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    #endregion
}
