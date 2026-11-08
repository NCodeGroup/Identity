// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Secrets.AzureKeyVault.Options;
using Xunit;

namespace NCode.Identity.Secrets.AzureKeyVault.Tests.Options;

public class AzureKeyVaultRemoteKeyProviderOptionsValidatorTests
{
    private static AzureKeyVaultRemoteKeyProviderOptionsValidator CreateValidator() => new();

    #region Validate Tests

    [Theory]
    [InlineData("https://contoso.vault.azure.net")]
    [InlineData("https://contoso.vault.azure.net/")]
    [InlineData("https://contoso.managedhsm.azure.net")]
    [InlineData("https://contoso.vault.azure.cn")]
    public void Validate_VaultUri_Succeeds(string vaultUri)
    {
        var validator = CreateValidator();

        var result = validator.Validate(
            null,
            new AzureKeyVaultRemoteKeyProviderOptions { VaultUri = vaultUri }
        );

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("contoso.vault.azure.net")]
    [InlineData("http://contoso.vault.azure.net")]
    [InlineData("https://contoso")]
    [InlineData("https://contoso.vault.azure.net/keys/ncode")]
    [InlineData("https://contoso.vault.azure.net\n")]
    [InlineData("https://contoso.vault.azure.net:8443")]
    [InlineData("https://user@contoso.vault.azure.net")]
    public void Validate_InvalidVaultUri_Fails(string? vaultUri)
    {
        var validator = CreateValidator();

        var result = validator.Validate(
            null,
            new AzureKeyVaultRemoteKeyProviderOptions { VaultUri = vaultUri }
        );

        Assert.True(result.Failed);
        Assert.Contains("VaultUri", result.FailureMessage);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Validate_HardwareProtected_DoesNotAffectTheResult(bool hardwareProtected)
    {
        var validator = CreateValidator();

        var result = validator.Validate(
            null,
            new AzureKeyVaultRemoteKeyProviderOptions
            {
                VaultUri = "https://contoso.vault.azure.net",
                HardwareProtected = hardwareProtected,
            }
        );

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_NullOptions_Throws()
    {
        var validator = CreateValidator();

        Assert.Throws<ArgumentNullException>(() => validator.Validate(null, null!));
    }

    #endregion
}
