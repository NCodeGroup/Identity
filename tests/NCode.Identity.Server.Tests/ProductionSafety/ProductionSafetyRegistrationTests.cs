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
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace NCode.Identity.Server.Tests;

public class ProductionSafetyRegistrationTests
{
    #region Helpers

    private static IConfiguration CreateConfiguration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                values.Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value))
            )
            .Build();

    /// <summary>
    /// A configuration an operator would deploy: an explicit host filter, no developer keys, no seeding.
    /// </summary>
    private static IConfiguration CreateSafeConfiguration() =>
        CreateConfiguration(
            ("AllowedHosts", "op.example.com"),
            ("DeveloperKeys:Mode", "None"),
            ("Cors:AllowedOrigins:0", "https://app.example.com")
        );

    private static IHostEnvironment CreateEnvironment(string environmentName)
    {
        var mock = new Mock<IHostEnvironment>(MockBehavior.Strict);
        mock.SetupGet(environment => environment.EnvironmentName)
            .Returns(environmentName)
            .Verifiable();
        return mock.Object;
    }

    #endregion

    #region AddProductionSafety

    [Fact]
    public void AddProductionSafety_WhenProductionAndUnsafe_Throws()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddProductionSafety(
                CreateConfiguration(("AllowedHosts", "*")),
                CreateEnvironment(Environments.Production)
            )
        );

        Assert.Contains("Refusing to start in the Production environment", exception.Message);
    }

    [Fact]
    public void AddProductionSafety_WhenProductionAndSafe_DoesNotThrow()
    {
        var services = new ServiceCollection();

        var result = services.AddProductionSafety(
            CreateSafeConfiguration(),
            CreateEnvironment(Environments.Production)
        );

        Assert.Same(services, result);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData("Conformance")]
    public void AddProductionSafety_WhenNotProduction_IgnoresUnsafeConfiguration(
        string environmentName
    )
    {
        var services = new ServiceCollection();

        // The same configuration that fails under Production above.
        var result = services.AddProductionSafety(
            CreateConfiguration(("AllowedHosts", "*")),
            CreateEnvironment(environmentName)
        );

        Assert.Same(services, result);
    }

    [Fact]
    public void AddProductionSafety_WhenProductionAndMultipleProblems_ReportsAllAtOnce()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(
            ("AllowedHosts", "*"),
            ("DeveloperKeys:Mode", "PersistentSigningKey"),
            ("DeveloperInteractiveSeed:Enabled", "true")
        );

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddProductionSafety(configuration, CreateEnvironment(Environments.Production))
        );

        // An operator should be able to fix every problem in one pass, not one restart per problem.
        Assert.Contains("AllowedHosts", exception.Message);
        Assert.Contains("DeveloperKeys:Mode", exception.Message);
        Assert.Contains("DeveloperInteractiveSeed", exception.Message);
    }

    #endregion

    #region FindViolations

    [Fact]
    public void FindViolations_WhenSafe_ReturnsEmpty()
    {
        var violations = ProductionSafetyRegistration.FindViolations(CreateSafeConfiguration());

        Assert.Empty(violations);
    }

    [Fact]
    public void FindViolations_WhenDeveloperKeyModeUnset_StillReportsEphemeralDefault()
    {
        // Absent configuration is NOT safe: the mode defaults to Ephemeral, so a deployment that simply never
        // mentions DeveloperKeys would otherwise pass while regenerating its signing key on every restart.
        var violations = ProductionSafetyRegistration.FindViolations(
            CreateConfiguration(("AllowedHosts", "op.example.com"))
        );

        var violation = Assert.Single(violations);
        Assert.Equal("DeveloperKeys:Mode", violation.ConfigurationKey);
        Assert.Contains("Ephemeral", violation.Problem);
    }

    [Theory]
    [InlineData("Ephemeral")]
    [InlineData("PersistentSigningKey")]
    public void FindViolations_WhenAnyDeveloperKeyMode_ReportsViolation(string mode)
    {
        var violations = ProductionSafetyRegistration.FindViolations(
            CreateConfiguration(("AllowedHosts", "op.example.com"), ("DeveloperKeys:Mode", mode))
        );

        Assert.Contains(
            violations,
            violation => violation.ConfigurationKey == "DeveloperKeys:Mode"
        );
    }

    [Theory]
    [InlineData("None")]
    [InlineData("none")]
    public void FindViolations_WhenDeveloperKeysDisabled_DoesNotReportKeyViolation(string mode)
    {
        // 'None' is the operator asserting they supply their own managed signing key, which is the only safe answer.
        var violations = ProductionSafetyRegistration.FindViolations(
            CreateConfiguration(("AllowedHosts", "op.example.com"), ("DeveloperKeys:Mode", mode))
        );

        Assert.DoesNotContain(
            violations,
            violation => violation.ConfigurationKey == "DeveloperKeys:Mode"
        );
    }

    [Theory]
    [InlineData("*")]
    [InlineData("")]
    [InlineData("op.example.com;*")]
    public void FindViolations_WhenAllowedHostsAcceptsAnything_ReportsViolation(string allowedHosts)
    {
        var violations = ProductionSafetyRegistration.FindViolations(
            CreateConfiguration(("AllowedHosts", allowedHosts), ("DeveloperKeys:Mode", "None"))
        );

        Assert.Contains(violations, violation => violation.ConfigurationKey == "AllowedHosts");
    }

    [Fact]
    public void FindViolations_WhenAllowedHostsIsExplicit_DoesNotReportHostViolation()
    {
        var violations = ProductionSafetyRegistration.FindViolations(
            CreateConfiguration(
                ("AllowedHosts", "op.example.com;id.example.com"),
                ("DeveloperKeys:Mode", "None")
            )
        );

        Assert.DoesNotContain(
            violations,
            violation => violation.ConfigurationKey == "AllowedHosts"
        );
    }

    [Theory]
    [InlineData("http://localhost:5173")]
    [InlineData("https://127.0.0.1:5173")]
    [InlineData("https://host.docker.internal:5001")]
    public void FindViolations_WhenCorsOriginIsLoopback_ReportsViolation(string origin)
    {
        var violations = ProductionSafetyRegistration.FindViolations(
            CreateConfiguration(
                ("AllowedHosts", "op.example.com"),
                ("DeveloperKeys:Mode", "None"),
                ("Cors:AllowedOrigins:0", origin)
            )
        );

        Assert.Contains(
            violations,
            violation => violation.ConfigurationKey == "Cors:AllowedOrigins"
        );
    }

    [Fact]
    public void FindViolations_WhenCorsOriginIsPublic_DoesNotReportCorsViolation()
    {
        var violations = ProductionSafetyRegistration.FindViolations(
            CreateConfiguration(
                ("AllowedHosts", "op.example.com"),
                ("DeveloperKeys:Mode", "None"),
                ("Cors:AllowedOrigins:0", "https://app.example.com")
            )
        );

        Assert.DoesNotContain(
            violations,
            violation => violation.ConfigurationKey == "Cors:AllowedOrigins"
        );
    }

    [Theory]
    [InlineData("DeveloperInteractiveSeed")]
    [InlineData("ConformanceSeed")]
    public void FindViolations_WhenSeedSectionPresent_ReportsViolation(string sectionName)
    {
        var violations = ProductionSafetyRegistration.FindViolations(
            CreateConfiguration(
                ("AllowedHosts", "op.example.com"),
                ("DeveloperKeys:Mode", "None"),
                ($"{sectionName}:Enabled", "true")
            )
        );

        Assert.Contains(violations, violation => violation.ConfigurationKey == sectionName);
    }

    #endregion
}
