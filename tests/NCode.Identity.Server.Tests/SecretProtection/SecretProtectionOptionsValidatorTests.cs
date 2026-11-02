// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Server.SecretProtection;
using Xunit;

namespace NCode.Identity.Server.Tests;

public sealed class SecretProtectionOptionsValidatorTests
{
    private static SecretProtectionOptionsValidator Validator { get; } = new();

    #region Validate Tests

    [Fact]
    public void Validate_WhenRootKeyProviderIsSet_Succeeds()
    {
        var result = Validator.Validate(
            name: null,
            new SecretProtectionOptions { RootKeyProvider = "local" }
        );

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenRootKeyProviderIsNotSet_FailsNamingTheKeyAndSayingItIsNotSet()
    {
        var result = Validator.Validate(name: null, new SecretProtectionOptions());

        Assert.True(result.Failed);
        Assert.Contains(
            "SecretProtection:RootKeyProvider is required, but it is not set.",
            result.FailureMessage,
            StringComparison.Ordinal
        );
        Assert.Contains("'ephemeral'", result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenRootKeyProviderIsBlank_FailsNamingTheKeyAndTheActualValue(
        string rootKeyProvider
    )
    {
        var result = Validator.Validate(
            name: null,
            new SecretProtectionOptions { RootKeyProvider = rootKeyProvider }
        );

        Assert.True(result.Failed);
        Assert.Contains(
            $"SecretProtection:RootKeyProvider is required, but it is blank ('{rootKeyProvider}').",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    #endregion
}
