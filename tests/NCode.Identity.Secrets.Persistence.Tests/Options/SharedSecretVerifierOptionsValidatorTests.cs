// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NCode.Identity.Secrets.Persistence.Verifiers;
using NCode.Identity.Secrets.Verifiers;
using NCode.Registration;

namespace NCode.Identity.Secrets.Persistence.Options;

public class SharedSecretVerifierOptionsValidatorTests
{
    private static SharedSecretVerifierOptionsValidator CreateValidator() =>
        new([new DefaultSecretVerifierSchemeDataSource()]);

    #region Validate Tests

    [Fact]
    public void Validate_WhenTheOptionsAreTheDefaults_Succeeds()
    {
        var result = CreateValidator().Validate(null, new SharedSecretVerifierOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenEitherSchemeIsRegistered_Succeeds()
    {
        var options = new SharedSecretVerifierOptions
        {
            GeneratedSecretScheme = SecretVerifierSchemes.Pbkdf2Sha256,
            SuppliedSecretScheme = SecretVerifierSchemes.Sha256,
        };

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenAPackageDescribesAScheme_Succeeds()
    {
        var validator = new SharedSecretVerifierOptionsValidator([
            new DefaultSecretVerifierSchemeDataSource(),
            TestSecretVerifierSchemes.CreateDataSource("package-scheme"),
        ]);
        var options = new SharedSecretVerifierOptions { SuppliedSecretScheme = "package-scheme" };

        var result = validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenNoSchemeIsRegistered_ReportsEachOne()
    {
        var options = new SharedSecretVerifierOptions
        {
            GeneratedSecretScheme = "unregistered",
            SuppliedSecretScheme = "",
        };

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Collection(
            result.Failures,
            failure =>
                Assert.Contains(
                    "'SharedSecretVerifierOptions.GeneratedSecretScheme' is 'unregistered'",
                    failure,
                    StringComparison.Ordinal
                ),
            failure =>
                Assert.Contains(
                    "'SharedSecretVerifierOptions.SuppliedSecretScheme' is ''",
                    failure,
                    StringComparison.Ordinal
                )
        );
    }

    // Builds the services and validates the options as a host does when it starts.
    private static void ValidateOnStart(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        new ServiceBuilder<SecretsLibrary>(services).AddPersistenceLibrary();
        configure(services);
        using var serviceProvider = services.BuildServiceProvider();
        serviceProvider.GetRequiredService<IStartupValidator>().Validate();
    }

    [Fact]
    public void AddPersistenceLibrary_WhenASchemeIsNotRegistered_FailsAsTheHostStarts()
    {
        var exception = Assert.Throws<OptionsValidationException>(() =>
            ValidateOnStart(services =>
                services.Configure<SharedSecretVerifierOptions>(options =>
                    options.SuppliedSecretScheme = "unregistered"
                )
            )
        );

        var failure = Assert.Single(exception.Failures);
        Assert.Contains(
            "'SharedSecretVerifierOptions.SuppliedSecretScheme' is 'unregistered'",
            failure,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void AddPersistenceLibrary_WhenThePbkdf2IterationsAreBelowOne_FailsAsTheHostStarts()
    {
        var exception = Assert.Throws<OptionsValidationException>(() =>
            ValidateOnStart(services =>
                services.Configure<Pbkdf2SecretVerifierOptions>(options => options.Iterations = 0)
            )
        );

        var failure = Assert.Single(exception.Failures);
        Assert.Equal(
            "'Pbkdf2SecretVerifierOptions.Iterations' is 0; it must be at least 1.",
            failure
        );
    }

    [Fact]
    public void AddPersistenceLibrary_WhenTheOptionsAreTheDefaults_StartsCleanly()
    {
        ValidateOnStart(_ => { });
    }

    #endregion
}
