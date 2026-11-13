// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Management;
using NCode.Identity.Registration;

namespace NCode.Identity.OpenId.Setup;

public class SetupOptionsValidatorTests
{
    private SetupOptionsValidator Validator { get; } = new();

    #region Validate Tests

    [Fact]
    public void Validate_WhenSectionIsEmpty_Succeeds()
    {
        var result = Validator.Validate(null, new SetupOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenTokenHasMinimumLength_Succeeds()
    {
        var options = new SetupOptions
        {
            Token = new string('a', SetupDefaults.MinimumTokenLength),
        };

        var result = Validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenTokenIsShorterThanMinimum_FailsNamingTheKey()
    {
        var options = new SetupOptions
        {
            Token = new string('a', SetupDefaults.MinimumTokenLength - 1),
        };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        var failure = Assert.Single(result.Failures!);
        Assert.Contains("'Setup:Token'", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenTokenIsEmpty_Fails()
    {
        var options = new SetupOptions { Token = string.Empty };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
    }

    [Theory]
    [InlineData(60)]
    [InlineData(3600)]
    [InlineData(24 * 60 * 60)]
    public void Validate_WhenLifetimeIsWithinBounds_Succeeds(int seconds)
    {
        var options = new SetupOptions { GeneratedTokenLifetime = TimeSpan.FromSeconds(seconds) };

        var result = Validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenLifetimeIsBelowOneMinute_FailsNamingTheKey()
    {
        var options = new SetupOptions { GeneratedTokenLifetime = TimeSpan.FromSeconds(59) };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        var failure = Assert.Single(result.Failures!);
        Assert.Contains("'Setup:GeneratedTokenLifetime'", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenLifetimeIsAboveTwentyFourHours_FailsNamingTheKey()
    {
        var options = new SetupOptions
        {
            GeneratedTokenLifetime = TimeSpan.FromHours(24) + TimeSpan.FromSeconds(1),
        };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        var failure = Assert.Single(result.Failures!);
        Assert.Contains("'Setup:GeneratedTokenLifetime'", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenRootNamesUserNameAndPassword_Succeeds()
    {
        var options = new SetupOptions
        {
            Root = new SetupRootOptions { UserName = "root", Password = "a-long-enough-password" },
        };

        var result = Validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenRootNamesUserNameWithoutPassword_FailsNamingTheKey()
    {
        var options = new SetupOptions { Root = new SetupRootOptions { UserName = "root" } };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        var failure = Assert.Single(result.Failures!);
        Assert.Contains("'Setup:Root'", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenRootNamesPasswordWithoutUserName_FailsNamingTheKey()
    {
        var options = new SetupOptions
        {
            Root = new SetupRootOptions { Password = "a-long-enough-password" },
        };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        var failure = Assert.Single(result.Failures!);
        Assert.Contains("'Setup:Root'", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenSeveralOptionsAreInvalid_ReportsEveryFailure()
    {
        var options = new SetupOptions
        {
            Token = "short",
            GeneratedTokenLifetime = TimeSpan.Zero,
            Root = new SetupRootOptions { UserName = "root" },
        };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Equal(3, result.Failures!.Count());
    }

    #endregion

    #region AddInstanceSetup Tests

    private static ServiceProvider BuildServiceProvider(
        Dictionary<string, string?> settings,
        Mock<IHostEnvironment> mockHostEnvironment
    )
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var serviceCollection = new ServiceCollection();
        serviceCollection
            .NewBuilder<OpenIdManagementLibrary>()
            .AddInstanceSetup(configuration, mockHostEnvironment.Object);

        return serviceCollection.BuildServiceProvider();
    }

    private static Mock<IHostEnvironment> CreateHostEnvironment(string contentRootPath)
    {
        var mockHostEnvironment = new Mock<IHostEnvironment>(MockBehavior.Strict);
        mockHostEnvironment.SetupGet(x => x.ContentRootPath).Returns(contentRootPath).Verifiable();
        return mockHostEnvironment;
    }

    [Fact]
    public void AddInstanceSetup_WhenBundlePathIsRelative_ResolvesItAgainstTheContentRoot()
    {
        var contentRootPath = Path.Combine(Path.GetTempPath(), "content-root");
        var mockHostEnvironment = CreateHostEnvironment(contentRootPath);
        using var serviceProvider = BuildServiceProvider(
            new Dictionary<string, string?> { ["Setup:BundleRootPath"] = "setup-bundle" },
            mockHostEnvironment
        );

        var options = serviceProvider.GetRequiredService<IOptions<SetupOptions>>().Value;

        Assert.Equal(Path.Combine(contentRootPath, "setup-bundle"), options.BundleRootPath);
        mockHostEnvironment.Verify();
    }

    [Fact]
    public void AddInstanceSetup_WhenSectionIsAbsent_BindsTheDefaults()
    {
        var mockHostEnvironment = CreateHostEnvironment(Path.GetTempPath());
        using var serviceProvider = BuildServiceProvider([], mockHostEnvironment);

        var options = serviceProvider.GetRequiredService<IOptions<SetupOptions>>().Value;

        Assert.Null(options.Token);
        Assert.Null(options.BundleRootPath);
        Assert.Equal(TimeSpan.FromHours(1), options.GeneratedTokenLifetime);
        mockHostEnvironment.Verify();
    }

    [Fact]
    public void AddInstanceSetup_WhenSectionHasAnUnknownKey_FailsWhenOptionsAreResolved()
    {
        var mockHostEnvironment = CreateHostEnvironment(Path.GetTempPath());
        using var serviceProvider = BuildServiceProvider(
            new Dictionary<string, string?> { ["Setup:Tokan"] = "misspelled" },
            mockHostEnvironment
        );

        var exception = Assert.Throws<InvalidOperationException>(() =>
            serviceProvider.GetRequiredService<IOptions<SetupOptions>>().Value
        );

        Assert.Contains("Setup", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddInstanceSetup_WhenTokenIsTooShort_FailsWhenOptionsAreResolved()
    {
        var mockHostEnvironment = CreateHostEnvironment(Path.GetTempPath());
        using var serviceProvider = BuildServiceProvider(
            new Dictionary<string, string?> { ["Setup:Token"] = "short" },
            mockHostEnvironment
        );

        var exception = Assert.Throws<OptionsValidationException>(() =>
            serviceProvider.GetRequiredService<IOptions<SetupOptions>>().Value
        );

        Assert.Contains("Setup:Token", exception.Message, StringComparison.Ordinal);
        mockHostEnvironment.Verify();
    }

    [Fact]
    public void AddInstanceSetup_Always_RegistersTheSystemTimeProvider()
    {
        var mockHostEnvironment = CreateHostEnvironment(Path.GetTempPath());
        using var serviceProvider = BuildServiceProvider([], mockHostEnvironment);

        var timeProvider = serviceProvider.GetRequiredService<TimeProvider>();

        Assert.Same(TimeProvider.System, timeProvider);
    }

    #endregion
}
