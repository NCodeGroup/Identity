// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Secrets.KeyWrapping.AzureKeyVault.Options;
using NCode.Identity.Secrets.Persistence.Options;
using Xunit;

namespace NCode.Identity.Secrets.KeyWrapping.AzureKeyVault.Tests.Options;

public class AzureKeyVaultKeyWrapperOptionsValidatorTests
{
    private const string KeyId = "https://contoso.vault.azure.net/keys/ncode-identity-root";

    private const string SecretId = "https://contoso.vault.azure.net/secrets/ncode-identity-root";

    private static AzureKeyVaultKeyWrapperOptionsValidator CreateValidator(
        string? rootKeyProvider
    ) =>
        new(
            Microsoft.Extensions.Options.Options.Create(
                new SecretProtectionOptions { RootKeyProvider = rootKeyProvider }
            )
        );

    #region Validate Tests

    [Fact]
    public void Validate_OtherProviderSelected_Skips()
    {
        var validator = CreateValidator(KeyWrapperNames.Local);

        var result = validator.Validate(null, new AzureKeyVaultKeyWrapperOptions { KeyId = null });

        Assert.True(result.Skipped);
    }

    [Fact]
    public void Validate_MissingKeyId_Fails()
    {
        var validator = CreateValidator(KeyWrapperNames.AzureKeyVault);

        var result = validator.Validate(null, new AzureKeyVaultKeyWrapperOptions { KeyId = null });

        Assert.True(result.Failed);
        Assert.Contains("KeyId", result.FailureMessage);
    }

    [Fact]
    public void Validate_SecretId_Fails()
    {
        var validator = CreateValidator(KeyWrapperNames.AzureKeyVault);

        var result = validator.Validate(
            null,
            new AzureKeyVaultKeyWrapperOptions { KeyId = SecretId }
        );

        Assert.True(result.Failed);
        Assert.Contains("KeyId", result.FailureMessage);
    }

    [Fact]
    public void Validate_KeyId_Succeeds()
    {
        var validator = CreateValidator(KeyWrapperNames.AzureKeyVault);

        var result = validator.Validate(null, new AzureKeyVaultKeyWrapperOptions { KeyId = KeyId });

        Assert.True(result.Succeeded);
    }

    #endregion
}
