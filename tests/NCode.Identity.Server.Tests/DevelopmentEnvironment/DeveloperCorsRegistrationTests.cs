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

using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace NCode.Identity.Server.Tests;

public sealed class DeveloperCorsRegistrationTests : BaseTests
{
    #region AddDeveloperCors Tests

    [Fact]
    public void AddDeveloperCors_WhenDevelopmentAndNoConfiguredOrigins_RegistersViteDefaults()
    {
        var policy = ResolvePolicy("Development", configuredOrigins: null);

        Assert.Contains("http://localhost:5173", policy.Origins);
        Assert.Contains("https://localhost:5173", policy.Origins);
    }

    [Fact]
    public void AddDeveloperCors_WhenConfiguredOrigins_UsesThemRegardlessOfEnvironment()
    {
        string[] expected = ["https://app.example"];

        var policy = ResolvePolicy("Production", configuredOrigins: expected);

        Assert.Equal(expected, policy.Origins);
    }

    [Fact]
    public void AddDeveloperCors_WhenNotDevelopmentAndNoConfiguredOrigins_RegistersEmptyPolicy()
    {
        var policy = ResolvePolicy("Production", configuredOrigins: null);

        Assert.Empty(policy.Origins);
    }

    #endregion

    private CorsPolicy ResolvePolicy(string environmentName, string[]? configuredOrigins)
    {
        var hostEnvironment = CreateStrictMock<IHostEnvironment>();
        hostEnvironment.Setup(x => x.EnvironmentName).Returns(environmentName);

        var configurationData = new Dictionary<string, string?>();
        if (configuredOrigins is not null)
        {
            for (var index = 0; index < configuredOrigins.Length; index++)
            {
                configurationData[$"Cors:AllowedOrigins:{index}"] = configuredOrigins[index];
            }
        }
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData)
            .Build();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddDeveloperCors(configuration, hostEnvironment.Object);

        using var serviceProvider = serviceCollection.BuildServiceProvider();
        var corsOptions = serviceProvider.GetRequiredService<IOptions<CorsOptions>>().Value;

        return corsOptions.GetPolicy(DeveloperCorsRegistration.PolicyName)
            ?? throw new InvalidOperationException(
                "Expected the developer CORS policy to be registered."
            );
    }
}
