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

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
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

    [Theory]
    [InlineData("https://app.example.com/")]
    [InlineData("https://app.example.com/path")]
    [InlineData("https://*.example.com")]
    [InlineData("*")]
    [InlineData("app.example.com")]
    public void AddDeveloperCors_WhenOriginCanNeverMatch_Throws(string origin)
    {
        var exception = Assert.Throws<OptionsValidationException>(() =>
            ResolvePolicy("Development", configuredOrigins: [origin])
        );

        Assert.Contains(
            $"Cors:AllowedOrigins:0 '{origin}'",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void AddDeveloperCors_WhenSectionHasUnknownKey_Throws()
    {
        var hostEnvironment = CreateEnvironment("Development");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["Cors:AllowedOrigin:0"] = "https://app.example" }
            )
            .Build();
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddDeveloperCors(configuration, hostEnvironment);
        using var serviceProvider = serviceCollection.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            serviceProvider.GetRequiredService<IOptions<CorsOptions>>().Value
        );

        Assert.Contains("'AllowedOrigin'", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    #region UseDeveloperCors Tests

    [Fact]
    public void UseDeveloperCors_WhenPolicyNotRegistered_AddsNoMiddleware()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddOptions();
        using var serviceProvider = serviceCollection.BuildServiceProvider();

        // Strict: the mock has no Use(...) setup, so adding middleware would fail the test — which is exactly what a
        // Production host, where the developer policy is never registered, must not do.
        var app = CreateStrictMock<IApplicationBuilder>();
        app.Setup(x => x.ApplicationServices).Returns(serviceProvider).Verifiable();

        var result = app.Object.UseDeveloperCors();

        Assert.Same(app.Object, result);
    }

    [Fact]
    public void UseDeveloperCors_WhenPolicyRegistered_AddsMiddleware()
    {
        var configuration = new ConfigurationBuilder().Build();
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddDeveloperCors(configuration, CreateEnvironment("Development"));
        using var serviceProvider = serviceCollection.BuildServiceProvider();

        var app = CreateStrictMock<IApplicationBuilder>();
        app.Setup(x => x.ApplicationServices).Returns(serviceProvider).Verifiable();
        app.Setup(x => x.Use(It.IsAny<Func<RequestDelegate, RequestDelegate>>()))
            .Returns(app.Object)
            .Verifiable();

        var result = app.Object.UseDeveloperCors();

        Assert.Same(app.Object, result);
    }

    #endregion

    private IHostEnvironment CreateEnvironment(string environmentName)
    {
        var hostEnvironment = CreateStrictMock<IHostEnvironment>();
        hostEnvironment.Setup(x => x.EnvironmentName).Returns(environmentName);
        return hostEnvironment.Object;
    }

    private CorsPolicy ResolvePolicy(string environmentName, string[]? configuredOrigins)
    {
        var hostEnvironment = CreateEnvironment(environmentName);

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
        serviceCollection.AddDeveloperCors(configuration, hostEnvironment);

        using var serviceProvider = serviceCollection.BuildServiceProvider();
        var corsOptions = serviceProvider.GetRequiredService<IOptions<CorsOptions>>().Value;

        return corsOptions.GetPolicy(DeveloperCorsRegistration.PolicyName)
            ?? throw new InvalidOperationException(
                "Expected the developer CORS policy to be registered."
            );
    }
}
