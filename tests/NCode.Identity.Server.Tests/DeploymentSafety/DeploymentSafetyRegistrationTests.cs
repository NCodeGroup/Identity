// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Server.DeploymentSafety;
using Xunit;

namespace NCode.Identity.Server.Tests;

public class DeploymentSafetyRegistrationTests
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
    private static (string Key, string Value)[] SafeSecretProtectionSettings { get; } =
    [
        ("SecretProtection:RootKeyProvider", "local"),
        ("SecretProtection:RootKeyProviders:Local:KeyPath", "/run/secrets/root-key"),
        ("SecretProtection:Recovery:Disabled", "true"),
    ];

    /// <summary>
    /// A configuration whose secret protection is safe, plus <paramref name="values"/>.
    /// </summary>
    private static IConfiguration CreateDeploymentConfiguration(
        params (string Key, string Value)[] values
    ) => CreateConfiguration([.. values, .. SafeSecretProtectionSettings]);

    /// <summary>
    /// A configuration an operator would deploy: an explicit host filter, no developer keys, no seeding, and a safe
    /// secret protection configuration.
    /// </summary>
    private static IConfiguration CreateSafeConfiguration() =>
        CreateDeploymentConfiguration(("AllowedHosts", "op.example.com"));

    /// <summary>
    /// A configuration that turns on the local inner-loop switch, with a host filter and no secret protection.
    /// </summary>
    private static IConfiguration CreateLocalDevelopmentConfiguration() =>
        CreateConfiguration(("LocalDevelopment:Enabled", "true"), ("AllowedHosts", "*"));

    private static IHostEnvironment CreateEnvironment(string environmentName)
    {
        var mock = new Mock<IHostEnvironment>(MockBehavior.Strict);
        mock.SetupGet(environment => environment.EnvironmentName)
            .Returns(environmentName)
            .Verifiable();
        return mock.Object;
    }

    /// <summary>
    /// A service collection that hosts the registrations under test in the environment named
    /// <paramref name="environmentName"/>.
    /// </summary>
    private static ServiceCollection CreateServiceCollection(string environmentName)
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(CreateEnvironment(environmentName));
        return serviceCollection;
    }

    #endregion

    #region AddDeploymentSafety

    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData("Conformance")]
    public void AddDeploymentSafety_WhenLocalDevelopmentIsOffAndUnsafe_Throws(
        string environmentName
    )
    {
        var serviceCollection = CreateServiceCollection(environmentName);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            serviceCollection.AddDeploymentSafety(CreateConfiguration(("AllowedHosts", "*")))
        );

        Assert.Contains("Refusing to start a deployment", exception.Message);
        Assert.Contains("LocalDevelopment:Enabled", exception.Message);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    public void AddDeploymentSafety_WhenLocalDevelopmentIsOffAndADeveloperSectionIsPresent_Throws(
        string environmentName
    )
    {
        var serviceCollection = CreateServiceCollection(environmentName);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            serviceCollection.AddDeploymentSafety(
                CreateDeploymentConfiguration(
                    ("AllowedHosts", "op.example.com"),
                    ("DeveloperKeys:PruneUnreadableKeys", "true")
                )
            )
        );

        Assert.Contains("- DeveloperKeys: ", exception.Message);
    }

    [Fact]
    public void AddDeploymentSafety_WhenLocalDevelopmentIsOffAndSafe_DoesNotThrow()
    {
        var serviceCollection = CreateServiceCollection(Environments.Production);

        var result = serviceCollection.AddDeploymentSafety(CreateSafeConfiguration());

        Assert.Same(serviceCollection, result);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData("Conformance")]
    public void AddDeploymentSafety_WhenLocalDevelopmentIsOn_IgnoresUnsafeConfiguration(
        string environmentName
    )
    {
        var serviceCollection = CreateServiceCollection(environmentName);

        // The same configuration that fails with the switch off above.
        var result = serviceCollection.AddDeploymentSafety(CreateLocalDevelopmentConfiguration());

        Assert.Same(serviceCollection, result);
    }

    [Fact]
    public void AddDeploymentSafety_WhenLocalDevelopmentIsOff_RegistersTheTransportSecurityCheck()
    {
        var serviceCollection = CreateServiceCollection(Environments.Production);

        serviceCollection.AddDeploymentSafety(CreateSafeConfiguration());

        Assert.Single(
            serviceCollection,
            descriptor =>
                descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType?.Name == "TransportSecurityStartupCheck"
        );
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData("Conformance")]
    public void AddDeploymentSafety_WhenLocalDevelopmentIsOffInAnyEnvironment_RegistersTheTransportSecurityCheck(
        string environmentName
    )
    {
        var serviceCollection = CreateServiceCollection(environmentName);

        serviceCollection.AddDeploymentSafety(CreateSafeConfiguration());

        Assert.Single(
            serviceCollection,
            descriptor =>
                descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType?.Name == "TransportSecurityStartupCheck"
        );
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData("Conformance")]
    public void AddDeploymentSafety_WhenLocalDevelopmentIsOn_RegistersNoTransportSecurityCheck(
        string environmentName
    )
    {
        var serviceCollection = CreateServiceCollection(environmentName);

        serviceCollection.AddDeploymentSafety(CreateLocalDevelopmentConfiguration());

        Assert.DoesNotContain(
            serviceCollection,
            descriptor => descriptor.ImplementationType?.Name == "TransportSecurityStartupCheck"
        );
    }

    [Fact]
    public void AddDeploymentSafety_WhenLocalDevelopmentIsOffAndMultipleProblems_ReportsAllAtOnce()
    {
        var serviceCollection = CreateServiceCollection(Environments.Production);
        var configuration = CreateConfiguration(
            ("AllowedHosts", "*"),
            ("DeveloperKeys:PruneUnreadableKeys", "true"),
            ("DeveloperInteractiveSeed:Enabled", "true")
        );

        var exception = Assert.Throws<InvalidOperationException>(() =>
            serviceCollection.AddDeploymentSafety(configuration)
        );

        // An operator should be able to fix every problem in one pass, not one restart per problem.
        Assert.Contains("AllowedHosts", exception.Message);
        Assert.Contains("DeveloperKeys", exception.Message);
        Assert.Contains("DeveloperInteractiveSeed", exception.Message);
    }

    [Fact]
    public void AddDeploymentSafety_WhenLocalDevelopmentIsOffAndSecretProtectionMissing_Throws()
    {
        var serviceCollection = CreateServiceCollection(Environments.Production);
        var configuration = CreateConfiguration(
            ("AllowedHosts", "op.example.com"),
            ("SecretProtection:RootKeyProviders:Local:Key", "inline-root-key-value")
        );

        var exception = Assert.Throws<InvalidOperationException>(() =>
            serviceCollection.AddDeploymentSafety(configuration)
        );

        Assert.Contains(
            "Refusing to start a deployment: 3 setting(s) are unsafe for a deployment.",
            exception.Message
        );
        Assert.Contains("- SecretProtection:RootKeyProvider: ", exception.Message);
        Assert.Contains("- SecretProtection:RootKeyProviders:Local:Key: ", exception.Message);
        Assert.Contains("- SecretProtection:Recovery:PublicKeyPath: ", exception.Message);
        Assert.DoesNotContain("inline-root-key-value", exception.Message);
    }

    [Fact]
    public void AddDeploymentSafety_WhenLocalDevelopmentIsOn_IgnoresMissingSecretProtection()
    {
        var serviceCollection = CreateServiceCollection(Environments.Development);

        var result = serviceCollection.AddDeploymentSafety(
            CreateConfiguration(
                ("LocalDevelopment:Enabled", "true"),
                ("AllowedHosts", "op.example.com")
            )
        );

        Assert.Same(serviceCollection, result);
    }

    [Fact]
    public void AddDeploymentSafety_WhenLocalDevelopmentIsOffAndKeyRingUnprotected_FailsOnStart()
    {
        var serviceCollection = CreateServiceCollection(Environments.Production);
        serviceCollection.AddDeploymentSafety(CreateSafeConfiguration());
        serviceCollection.Configure<KeyManagementOptions>(options =>
            options.XmlRepository = new Mock<IXmlRepository>(MockBehavior.Strict).Object
        );
        using var serviceProvider = serviceCollection.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(() =>
            serviceProvider.GetRequiredService<IStartupValidator>().Validate()
        );

        Assert.Equal([DeploymentSafetyRegistration.KeyRingUnprotectedMessage], exception.Failures);
    }

    [Fact]
    public void AddDeploymentSafety_WhenLocalDevelopmentIsOffAndKeyRingHasNoRepository_FailsOnStart()
    {
        var serviceCollection = CreateServiceCollection(Environments.Production);
        serviceCollection.AddDeploymentSafety(CreateSafeConfiguration());
        using var serviceProvider = serviceCollection.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(() =>
            serviceProvider.GetRequiredService<IStartupValidator>().Validate()
        );

        Assert.Equal(
            [
                DeploymentSafetyRegistration.KeyRingRepositoryMissingMessage,
                DeploymentSafetyRegistration.KeyRingUnprotectedMessage,
            ],
            exception.Failures
        );
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    public void AddDeploymentSafety_WhenLocalDevelopmentIsOffInAnyEnvironmentAndKeyRingHasNoRepository_FailsOnStart(
        string environmentName
    )
    {
        var serviceCollection = CreateServiceCollection(environmentName);
        serviceCollection.AddDeploymentSafety(CreateSafeConfiguration());
        using var serviceProvider = serviceCollection.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(() =>
            serviceProvider.GetRequiredService<IStartupValidator>().Validate()
        );

        Assert.Contains(
            DeploymentSafetyRegistration.KeyRingRepositoryMissingMessage,
            exception.Failures
        );
    }

    [Fact]
    public void AddDeploymentSafety_WhenLocalDevelopmentIsOffAndPbkdf2IterationsAreBelowTheRecommendation_FailsOnStart()
    {
        var serviceCollection = CreateServiceCollection(Environments.Production);
        serviceCollection.AddDeploymentSafety(CreateSafeConfiguration());
        serviceCollection.Configure<KeyManagementOptions>(options =>
        {
            options.XmlRepository = new Mock<IXmlRepository>(MockBehavior.Strict).Object;
            options.XmlEncryptor = new Mock<IXmlEncryptor>(MockBehavior.Strict).Object;
        });
        serviceCollection.Configure<Pbkdf2SecretVerifierOptions>(options =>
            options.Iterations = Pbkdf2SecretVerifierOptions.MinimumDeploymentIterations - 1
        );
        using var serviceProvider = serviceCollection.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(() =>
            serviceProvider.GetRequiredService<IStartupValidator>().Validate()
        );

        Assert.Equal(
            [
                DeploymentPbkdf2IterationsValidator.CreateMessage(
                    Pbkdf2SecretVerifierOptions.MinimumDeploymentIterations - 1
                ),
            ],
            exception.Failures
        );
    }

    [Fact]
    public void AddDeploymentSafety_WhenLocalDevelopmentIsOn_AllowsAnyPbkdf2Iterations()
    {
        var serviceCollection = CreateServiceCollection(Environments.Development);
        serviceCollection.AddDeploymentSafety(CreateLocalDevelopmentConfiguration());
        serviceCollection.Configure<Pbkdf2SecretVerifierOptions>(options => options.Iterations = 1);
        using var serviceProvider = serviceCollection.BuildServiceProvider();

        serviceProvider.GetRequiredService<IStartupValidator>().Validate();
    }

    [Fact]
    public void AddDeploymentSafety_WhenLocalDevelopmentIsOffAndKeyRingProtected_StartsCleanly()
    {
        var serviceCollection = CreateServiceCollection(Environments.Production);
        serviceCollection.AddDeploymentSafety(CreateSafeConfiguration());
        serviceCollection.Configure<KeyManagementOptions>(options =>
        {
            options.XmlRepository = new Mock<IXmlRepository>(MockBehavior.Strict).Object;
            options.XmlEncryptor = new Mock<IXmlEncryptor>(MockBehavior.Strict).Object;
        });
        using var serviceProvider = serviceCollection.BuildServiceProvider();

        serviceProvider.GetRequiredService<IStartupValidator>().Validate();
    }

    [Fact]
    public void AddDeploymentSafety_WhenLocalDevelopmentIsOn_RegistersNoGate()
    {
        var serviceCollection = CreateServiceCollection(Environments.Development);

        serviceCollection.AddDeploymentSafety(CreateLocalDevelopmentConfiguration());

        // Only the switch's own options remain: no key ring, verifier, platform or transport check is registered.
        Assert.DoesNotContain(
            serviceCollection,
            descriptor =>
                descriptor.ServiceType == typeof(IValidateOptions<KeyManagementOptions>)
                || descriptor.ServiceType == typeof(IValidateOptions<Pbkdf2SecretVerifierOptions>)
                || descriptor.ServiceType == typeof(IValidateOptions<OpenIdOptions>)
                || descriptor.ServiceType == typeof(IHostedService)
        );
    }

    [Fact]
    public void AddDeploymentSafety_WhenLocalDevelopmentIsOffAndPlatformEnvironmentTypeIsNotProduction_FailsOnStart()
    {
        using var serviceProvider = BuildProtectedProvider("development", Environments.Production);

        var exception = Assert.Throws<OptionsValidationException>(() =>
            serviceProvider.GetRequiredService<IStartupValidator>().Validate()
        );

        var failure = Assert.Single(exception.Failures);
        Assert.Contains(DeploymentEnvironmentTypeValidator.ConfigurationKey, failure);
        Assert.Contains("'development'", failure);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    public void AddDeploymentSafety_WhenLocalDevelopmentIsOffInAnyEnvironmentAndPlatformEnvironmentTypeIsNotProduction_FailsOnStart(
        string environmentName
    )
    {
        using var serviceProvider = BuildProtectedProvider("development", environmentName);

        var exception = Assert.Throws<OptionsValidationException>(() =>
            serviceProvider.GetRequiredService<IStartupValidator>().Validate()
        );

        var failure = Assert.Single(exception.Failures);
        Assert.Contains(DeploymentEnvironmentTypeValidator.ConfigurationKey, failure);
    }

    [Theory]
    [InlineData("production")]
    [InlineData("unregistered")]
    [InlineData(null)]
    public void AddDeploymentSafety_WhenLocalDevelopmentIsOffAndPlatformEnvironmentTypeIsNotANonProductionType_StartsCleanly(
        string? typeName
    )
    {
        using var serviceProvider = BuildProtectedProvider(typeName, Environments.Production);

        serviceProvider.GetRequiredService<IStartupValidator>().Validate();
    }

    /// <summary>
    /// A provider for a host without the local inner-loop switch, with a protected key ring, the registered
    /// <c>production</c> and <c>development</c> types, and configured platform settings that select
    /// <paramref name="typeName"/> (or no type when it is null).
    /// </summary>
    private static ServiceProvider BuildProtectedProvider(string? typeName, string environmentName)
    {
        (string Key, string Value)[] settings = typeName is null
            ? []
            : [(DeploymentEnvironmentTypeValidator.ConfigurationKey, typeName)];
        var configuration = CreateConfiguration([
            .. settings,
            ("AllowedHosts", "op.example.com"),
            .. SafeSecretProtectionSettings,
        ]);
        var serviceCollection = CreateServiceCollection(environmentName);
        serviceCollection.AddDeploymentSafety(configuration);
        serviceCollection.Configure<KeyManagementOptions>(options =>
        {
            options.XmlRepository = new Mock<IXmlRepository>(MockBehavior.Strict).Object;
            options.XmlEncryptor = new Mock<IXmlEncryptor>(MockBehavior.Strict).Object;
        });
        serviceCollection.Configure<OpenIdOptions>(options =>
            options.Platform.Settings = configuration.GetSection("OpenId:Platform:Settings")
        );
        serviceCollection.AddSingleton<ITenantEnvironmentType>(
            new StubEnvironmentType("production", true)
        );
        serviceCollection.AddSingleton<ITenantEnvironmentType>(
            new StubEnvironmentType("development", false)
        );
        return serviceCollection.BuildServiceProvider();
    }

    private sealed class StubEnvironmentType(string typeName, bool isProduction)
        : ITenantEnvironmentType
    {
        public string TypeName { get; } = typeName;

        public bool IsProduction { get; } = isProduction;

        public IReadOnlyCollection<string> SecurityProfiles { get; } = [];
    }

    #endregion

    #region FindViolations

    [Fact]
    public void FindViolations_WhenSafe_ReturnsEmpty()
    {
        var violations = DeploymentSafetyRegistration.FindViolations(CreateSafeConfiguration());

        Assert.Empty(violations);
    }

    [Fact]
    public void FindViolations_WhenDeveloperKeysSectionIsPresent_ReportsIt()
    {
        var violations = DeploymentSafetyRegistration.FindViolations(
            CreateDeploymentConfiguration(
                ("AllowedHosts", "op.example.com"),
                ("DeveloperKeys:PruneUnreadableKeys", "true")
            )
        );

        var violation = Assert.Single(violations);
        Assert.Equal("DeveloperKeys", violation.ConfigurationKey);
    }

    [Fact]
    public void FindViolations_WhenDeveloperKeysSectionIsAbsent_ReportsNothingForKeys()
    {
        var violations = DeploymentSafetyRegistration.FindViolations(
            CreateDeploymentConfiguration(("AllowedHosts", "op.example.com"))
        );

        Assert.Empty(violations);
    }

    [Theory]
    [InlineData("*")]
    [InlineData("")]
    [InlineData("op.example.com;*")]
    public void FindViolations_WhenAllowedHostsAcceptsAnything_ReportsViolation(string allowedHosts)
    {
        var violations = DeploymentSafetyRegistration.FindViolations(
            CreateDeploymentConfiguration(("AllowedHosts", allowedHosts))
        );

        Assert.Contains(violations, violation => violation.ConfigurationKey == "AllowedHosts");
    }

    [Fact]
    public void FindViolations_WhenAllowedHostsIsExplicit_DoesNotReportHostViolation()
    {
        var violations = DeploymentSafetyRegistration.FindViolations(
            CreateDeploymentConfiguration(("AllowedHosts", "op.example.com;id.example.com"))
        );

        Assert.DoesNotContain(
            violations,
            violation => violation.ConfigurationKey == "AllowedHosts"
        );
    }

    [Theory]
    [InlineData("https://localhost:5173")]
    [InlineData("https://host.docker.internal:5001")]
    [InlineData("https://app.example.com")]
    public void FindViolations_WhenCorsSectionPresent_ReportsViolation(string origin)
    {
        // The Cors section is development-only: it admits cross-origin calls from the origins it lists, whether it
        // names a loopback dev server or a real origin.
        var violations = DeploymentSafetyRegistration.FindViolations(
            CreateDeploymentConfiguration(
                ("AllowedHosts", "op.example.com"),
                ("Cors:AllowedOrigins:0", origin)
            )
        );

        var violation = Assert.Single(violations);
        Assert.Equal("Cors", violation.ConfigurationKey);
    }

    [Theory]
    [InlineData("DeveloperInteractiveSeed")]
    [InlineData("ConformanceSeed")]
    public void FindViolations_WhenSeedSectionPresent_ReportsViolation(string sectionName)
    {
        var violations = DeploymentSafetyRegistration.FindViolations(
            CreateDeploymentConfiguration(
                ("AllowedHosts", "op.example.com"),
                ($"{sectionName}:Enabled", "true")
            )
        );

        Assert.Contains(violations, violation => violation.ConfigurationKey == sectionName);
    }

    [Fact]
    public void FindViolations_WhenBootstrapAdminSectionPresent_ReportsIt()
    {
        var violations = DeploymentSafetyRegistration.FindViolations(
            CreateDeploymentConfiguration(
                ("AllowedHosts", "op.example.com"),
                ("BootstrapAdmin:ClientSecret", "c2VjcmV0")
            )
        );

        var violation = Assert.Single(violations);
        Assert.Equal("BootstrapAdmin", violation.ConfigurationKey);
        Assert.Contains("GlobalAdmin", violation.Problem);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void FindViolations_WhenRootKeyProviderBlank_ReportsRootKeyProvider(string provider)
    {
        var violations = DeploymentSafetyRegistration.FindViolations(
            CreateConfiguration(
                ("AllowedHosts", "op.example.com"),
                ("SecretProtection:RootKeyProvider", provider),
                ("SecretProtection:Recovery:Disabled", "true")
            )
        );

        var violation = Assert.Single(violations);
        Assert.Equal("SecretProtection:RootKeyProvider", violation.ConfigurationKey);
        Assert.Contains("is not set", violation.Problem);
        Assert.DoesNotContain("ephemeral", violation.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public void FindViolations_WhenRootKeyProviderUnset_ReportsRootKeyProvider()
    {
        var violations = DeploymentSafetyRegistration.FindViolations(
            CreateConfiguration(
                ("AllowedHosts", "op.example.com"),
                ("SecretProtection:Recovery:Disabled", "true")
            )
        );

        var violation = Assert.Single(violations);
        Assert.Equal("SecretProtection:RootKeyProvider", violation.ConfigurationKey);
        Assert.Contains("is not set", violation.Problem);
        Assert.DoesNotContain("ephemeral", violation.Problem, StringComparison.Ordinal);
        Assert.Contains("'aws-kms'", violation.Resolution);
        Assert.Contains("'azure-key-vault'", violation.Resolution);
        Assert.Contains("'google-cloud-kms'", violation.Resolution);
    }

    [Fact]
    public void FindViolations_WhenRootKeyProviderEphemeral_ReportsRootKeyProvider()
    {
        var violations = DeploymentSafetyRegistration.FindViolations(
            CreateConfiguration(
                ("AllowedHosts", "op.example.com"),
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
        var violations = DeploymentSafetyRegistration.FindViolations(
            CreateConfiguration(
                ("AllowedHosts", "op.example.com"),
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
        var violations = DeploymentSafetyRegistration.FindViolations(
            CreateConfiguration(
                ("AllowedHosts", "op.example.com"),
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
        var violations = DeploymentSafetyRegistration.FindViolations(
            CreateConfiguration(
                ("AllowedHosts", "op.example.com"),
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
        var violations = DeploymentSafetyRegistration.FindViolations(
            CreateConfiguration(
                ("AllowedHosts", "op.example.com"),
                ("SecretProtection:RootKeyProvider", "local"),
                ("SecretProtection:Recovery:Disabled", "true")
            )
        );

        Assert.Empty(violations);
    }

    [Fact]
    public void FindViolations_WhenRecoveryPublicKeyConfigured_DoesNotReportRecovery()
    {
        var violations = DeploymentSafetyRegistration.FindViolations(
            CreateConfiguration(
                ("AllowedHosts", "op.example.com"),
                ("SecretProtection:RootKeyProvider", "local"),
                ("SecretProtection:Recovery:PublicKeyPath", "/run/secrets/recovery.pub")
            )
        );

        Assert.Empty(violations);
    }

    #endregion
}
