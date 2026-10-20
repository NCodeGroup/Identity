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
