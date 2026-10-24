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

using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
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
    /// The secret protection settings an operator would deploy: a root key provider whose key outlives the process,
    /// mounted from a file, with recovery explicitly declined.
    /// </summary>
    private static readonly (string Key, string Value)[] SafeSecretProtectionSettings =
    [
        ("SecretProtection:RootKeyProvider", "local"),
        ("SecretProtection:RootKeyProviders:Local:KeyPath", "/run/secrets/root-key"),
        ("SecretProtection:Recovery:Disabled", "true"),
    ];

    /// <summary>
    /// A configuration whose secret protection is safe, plus <paramref name="values"/>.
    /// </summary>
    private static IConfiguration CreateProductionConfiguration(
        params (string Key, string Value)[] values
    ) => CreateConfiguration([.. values, .. SafeSecretProtectionSettings]);

    /// <summary>
    /// A configuration an operator would deploy: an explicit host filter, no developer keys, no seeding, and a safe
    /// secret protection configuration.
    /// </summary>
    private static IConfiguration CreateSafeConfiguration() =>
        CreateProductionConfiguration(
            ("AllowedHosts", "op.example.com"),
            ("DeveloperKeys:Mode", "None")
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

    [Fact]
    public void AddProductionSafety_WhenProductionAndSecretProtectionMissing_Throws()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(
            ("AllowedHosts", "op.example.com"),
            ("DeveloperKeys:Mode", "None"),
            ("SecretProtection:RootKeyProviders:Local:Key", "inline-root-key-value")
        );

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddProductionSafety(configuration, CreateEnvironment(Environments.Production))
        );

        Assert.Contains(
            "Refusing to start in the Production environment: 3 setting(s) are unsafe for a production deployment.",
            exception.Message
        );
        Assert.Contains("- SecretProtection:RootKeyProvider: ", exception.Message);
        Assert.Contains("- SecretProtection:RootKeyProviders:Local:Key: ", exception.Message);
        Assert.Contains("- SecretProtection:Recovery:PublicKeyPath: ", exception.Message);
        Assert.DoesNotContain("inline-root-key-value", exception.Message);
    }

    [Fact]
    public void AddProductionSafety_WhenNotProduction_IgnoresMissingSecretProtection()
    {
        var services = new ServiceCollection();

        var result = services.AddProductionSafety(
            CreateConfiguration(("AllowedHosts", "op.example.com"), ("DeveloperKeys:Mode", "None")),
            CreateEnvironment(Environments.Development)
        );

        Assert.Same(services, result);
    }

    [Fact]
    public void AddProductionSafety_WhenProductionAndKeyRingUnprotected_FailsOnStart()
    {
        var services = new ServiceCollection();
        services.AddProductionSafety(
            CreateSafeConfiguration(),
            CreateEnvironment(Environments.Production)
        );
        services.Configure<KeyManagementOptions>(options =>
            options.XmlRepository = new Mock<IXmlRepository>(MockBehavior.Strict).Object
        );
        using var serviceProvider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(() =>
            serviceProvider.GetRequiredService<IStartupValidator>().Validate()
        );

        Assert.Equal([ProductionSafetyRegistration.KeyRingUnprotectedMessage], exception.Failures);
    }

    [Fact]
    public void AddProductionSafety_WhenProductionAndKeyRingHasNoRepository_FailsOnStart()
    {
        var services = new ServiceCollection();
        services.AddProductionSafety(
            CreateSafeConfiguration(),
            CreateEnvironment(Environments.Production)
        );
        using var serviceProvider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(() =>
            serviceProvider.GetRequiredService<IStartupValidator>().Validate()
        );

        Assert.Equal(
            [
                ProductionSafetyRegistration.KeyRingRepositoryMissingMessage,
                ProductionSafetyRegistration.KeyRingUnprotectedMessage,
            ],
            exception.Failures
        );
    }

    [Fact]
    public void AddProductionSafety_WhenProductionAndKeyRingProtected_StartsCleanly()
    {
        var services = new ServiceCollection();
        services.AddProductionSafety(
            CreateSafeConfiguration(),
            CreateEnvironment(Environments.Production)
        );
        services.Configure<KeyManagementOptions>(options =>
        {
            options.XmlRepository = new Mock<IXmlRepository>(MockBehavior.Strict).Object;
            options.XmlEncryptor = new Mock<IXmlEncryptor>(MockBehavior.Strict).Object;
        });
        using var serviceProvider = services.BuildServiceProvider();

        serviceProvider.GetRequiredService<IStartupValidator>().Validate();
    }

    [Fact]
    public void AddProductionSafety_WhenNotProduction_LeavesTheKeyRingUnchecked()
    {
        var services = new ServiceCollection();

        services.AddProductionSafety(
            CreateSafeConfiguration(),
            CreateEnvironment(Environments.Development)
        );

        Assert.Empty(services);
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
            CreateProductionConfiguration(("AllowedHosts", "op.example.com"))
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
            CreateProductionConfiguration(
                ("AllowedHosts", "op.example.com"),
                ("DeveloperKeys:Mode", mode)
            )
        );

        var violation = Assert.Single(
            violations,
            violation => violation.ConfigurationKey == "DeveloperKeys:Mode"
        );
        Assert.Contains("provisioned with persisted signing keys", violation.Resolution);
    }

    [Theory]
    [InlineData("None")]
    [InlineData("none")]
    public void FindViolations_WhenDeveloperKeysDisabled_DoesNotReportKeyViolation(string mode)
    {
        // 'None' leaves tenants with the persisted keys they are provisioned with, or the keys an operator imports.
        var violations = ProductionSafetyRegistration.FindViolations(
            CreateProductionConfiguration(
                ("AllowedHosts", "op.example.com"),
                ("DeveloperKeys:Mode", mode)
            )
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
            CreateProductionConfiguration(
                ("AllowedHosts", allowedHosts),
                ("DeveloperKeys:Mode", "None")
            )
        );

        Assert.Contains(violations, violation => violation.ConfigurationKey == "AllowedHosts");
    }

    [Fact]
    public void FindViolations_WhenAllowedHostsIsExplicit_DoesNotReportHostViolation()
    {
        var violations = ProductionSafetyRegistration.FindViolations(
            CreateProductionConfiguration(
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
    [InlineData("https://host.docker.internal:5001")]
    [InlineData("https://app.example.com")]
    public void FindViolations_WhenCorsSectionPresent_ReportsViolation(string origin)
    {
        // The developer CORS policy is never registered in Production, so any Cors section there is accepted without
        // being honored — whether it names a loopback dev server or a real origin.
        var violations = ProductionSafetyRegistration.FindViolations(
            CreateProductionConfiguration(
                ("AllowedHosts", "op.example.com"),
                ("DeveloperKeys:Mode", "None"),
                ("Cors:AllowedOrigins:0", origin)
            )
        );

        var violation = Assert.Single(violations);
        Assert.Equal("Cors", violation.ConfigurationKey);
    }

    [Theory]
    [InlineData("DeveloperKeys:Mode", "Persistent")]
    [InlineData("DeveloperKeys:Mode", "5")]
    [InlineData("DeveloperKeys:Mdoe", "None")]
    public void FindViolations_WhenDeveloperKeysCannotBeRead_ReportsTheSection(
        string key,
        string value
    )
    {
        var violations = ProductionSafetyRegistration.FindViolations(
            CreateProductionConfiguration(("AllowedHosts", "op.example.com"), (key, value))
        );

        var violation = Assert.Single(violations);
        Assert.Equal("DeveloperKeys", violation.ConfigurationKey);
    }

    [Theory]
    [InlineData("DeveloperInteractiveSeed")]
    [InlineData("ConformanceSeed")]
    public void FindViolations_WhenSeedSectionPresent_ReportsViolation(string sectionName)
    {
        var violations = ProductionSafetyRegistration.FindViolations(
            CreateProductionConfiguration(
                ("AllowedHosts", "op.example.com"),
                ("DeveloperKeys:Mode", "None"),
                ($"{sectionName}:Enabled", "true")
            )
        );

        Assert.Contains(violations, violation => violation.ConfigurationKey == sectionName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void FindViolations_WhenRootKeyProviderBlank_ReportsRootKeyProvider(string provider)
    {
        var violations = ProductionSafetyRegistration.FindViolations(
            CreateConfiguration(
                ("AllowedHosts", "op.example.com"),
                ("DeveloperKeys:Mode", "None"),
                ("SecretProtection:RootKeyProvider", provider),
                ("SecretProtection:Recovery:Disabled", "true")
            )
        );

        var violation = Assert.Single(violations);
        Assert.Equal("SecretProtection:RootKeyProvider", violation.ConfigurationKey);
        Assert.Contains("is not set", violation.Problem);
    }

    [Fact]
    public void FindViolations_WhenRootKeyProviderUnset_ReportsRootKeyProvider()
    {
        var violations = ProductionSafetyRegistration.FindViolations(
            CreateConfiguration(
                ("AllowedHosts", "op.example.com"),
                ("DeveloperKeys:Mode", "None"),
                ("SecretProtection:Recovery:Disabled", "true")
            )
        );

        var violation = Assert.Single(violations);
        Assert.Equal("SecretProtection:RootKeyProvider", violation.ConfigurationKey);
        Assert.Contains("is not set", violation.Problem);
        Assert.Contains("'aws-kms'", violation.Resolution);
        Assert.Contains("'azure-key-vault'", violation.Resolution);
        Assert.Contains("'google-cloud-kms'", violation.Resolution);
    }

    [Fact]
    public void FindViolations_WhenRootKeyProviderEphemeral_ReportsRootKeyProvider()
    {
        var violations = ProductionSafetyRegistration.FindViolations(
            CreateConfiguration(
                ("AllowedHosts", "op.example.com"),
                ("DeveloperKeys:Mode", "None"),
                ("SecretProtection:RootKeyProvider", "ephemeral"),
                ("SecretProtection:Recovery:Disabled", "true")
            )
        );

        var violation = Assert.Single(violations);
        Assert.Equal("SecretProtection:RootKeyProvider", violation.ConfigurationKey);
        Assert.Contains("'ephemeral' keeps the root key in memory", violation.Problem);
        Assert.Contains("'aws-kms'", violation.Resolution);
        Assert.Contains("'azure-key-vault'", violation.Resolution);
        Assert.Contains("'google-cloud-kms'", violation.Resolution);
    }

    [Theory]
    [InlineData("local")]
    [InlineData("aws-kms")]
    public void FindViolations_WhenLocalKeyInline_ReportsLocalKey(string provider)
    {
        // An inline root key is a leaked key even when another provider is the one selected.
        var violations = ProductionSafetyRegistration.FindViolations(
            CreateConfiguration(
                ("AllowedHosts", "op.example.com"),
                ("DeveloperKeys:Mode", "None"),
                ("SecretProtection:RootKeyProvider", provider),
                ("SecretProtection:RootKeyProviders:Local:Key", "inline-root-key-value"),
                ("SecretProtection:Recovery:Disabled", "true")
            )
        );

        var violation = Assert.Single(violations);
        Assert.Equal("SecretProtection:RootKeyProviders:Local:Key", violation.ConfigurationKey);
        Assert.Contains("inline", violation.Problem);
        Assert.Contains("Local:KeyPath", violation.Resolution);
        Assert.DoesNotContain("inline-root-key-value", violation.ToString());
    }

    [Fact]
    public void FindViolations_WhenRecoveryNeitherConfiguredNorDisabled_ReportsRecovery()
    {
        var violations = ProductionSafetyRegistration.FindViolations(
            CreateConfiguration(
                ("AllowedHosts", "op.example.com"),
                ("DeveloperKeys:Mode", "None"),
                ("SecretProtection:RootKeyProvider", "local")
            )
        );

        var violation = Assert.Single(violations);
        Assert.Equal("SecretProtection:Recovery:PublicKeyPath", violation.ConfigurationKey);
        Assert.Contains("losing the root key loses every stored secret", violation.Problem);
        Assert.Contains("SecretProtection:Recovery:Disabled", violation.Resolution);
    }

    [Theory]
    [InlineData("false")]
    [InlineData("maybe")]
    [InlineData("")]
    public void FindViolations_WhenRecoveryDisabledIsNotTrue_ReportsRecovery(string disabled)
    {
        // Only a value that parses as true declines recovery; anything else counts as not disabled.
        var violations = ProductionSafetyRegistration.FindViolations(
            CreateConfiguration(
                ("AllowedHosts", "op.example.com"),
                ("DeveloperKeys:Mode", "None"),
                ("SecretProtection:RootKeyProvider", "local"),
                ("SecretProtection:Recovery:Disabled", disabled)
            )
        );

        var violation = Assert.Single(violations);
        Assert.Equal("SecretProtection:Recovery:PublicKeyPath", violation.ConfigurationKey);
    }

    [Fact]
    public void FindViolations_WhenRecoveryDisabled_DoesNotReportRecovery()
    {
        var violations = ProductionSafetyRegistration.FindViolations(
            CreateConfiguration(
                ("AllowedHosts", "op.example.com"),
                ("DeveloperKeys:Mode", "None"),
                ("SecretProtection:RootKeyProvider", "local"),
                ("SecretProtection:Recovery:Disabled", "true")
            )
        );

        Assert.Empty(violations);
    }

    [Fact]
    public void FindViolations_WhenRecoveryPublicKeyConfigured_DoesNotReportRecovery()
    {
        var violations = ProductionSafetyRegistration.FindViolations(
            CreateConfiguration(
                ("AllowedHosts", "op.example.com"),
                ("DeveloperKeys:Mode", "None"),
                ("SecretProtection:RootKeyProvider", "local"),
                ("SecretProtection:Recovery:PublicKeyPath", "/run/secrets/recovery.pub")
            )
        );

        Assert.Empty(violations);
    }

    #endregion
}
