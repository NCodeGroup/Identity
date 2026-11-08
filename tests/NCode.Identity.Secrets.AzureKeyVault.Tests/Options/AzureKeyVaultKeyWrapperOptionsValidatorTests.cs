// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Secrets.AzureKeyVault.Options;
using NCode.Identity.Secrets.KeyWrapping;
using NCode.Identity.Secrets.Persistence.Options;
using Xunit;

namespace NCode.Identity.Secrets.AzureKeyVault.Tests.Options;

public class AzureKeyVaultKeyWrapperOptionsValidatorTests
{
    private const string KeyId = "https://contoso.vault.azure.net/keys/ncode-identity-root";

    private const string SecretId = "https://contoso.vault.azure.net/secrets/ncode-identity-root";

    private const string SuffixesKey =
        "SecretProtection:RootKeyProviders:AzureKeyVault:ReferencedKeyHostSuffixes";

    private static AzureKeyVaultKeyWrapperOptionsValidator CreateValidator(
        string? rootKeyProvider,
        params string[] ownerProviders
    ) =>
        new(
            Microsoft.Extensions.Options.Options.Create(
                new SecretProtectionOptions
                {
                    RootKeyProvider = rootKeyProvider,
                    OwnerRootKeys = new OwnerRootKeyOptions { Providers = ownerProviders },
                }
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

    [Fact]
    public void Validate_OwnerProviderButNotRoot_SkipsKeyIdAndValidatesSuffixes()
    {
        var validator = CreateValidator(KeyWrapperNames.Local, KeyWrapperNames.AzureKeyVault);

        var success = validator.Validate(null, new AzureKeyVaultKeyWrapperOptions { KeyId = null });
        var failure = validator.Validate(
            null,
            new AzureKeyVaultKeyWrapperOptions { ReferencedKeyHostSuffixes = [] }
        );

        Assert.True(success.Succeeded);
        Assert.True(failure.Failed);
        Assert.Contains(SuffixesKey, failure.FailureMessage);
    }

    [Fact]
    public void Validate_EmptySuffixesAndNotRootOrOwnerProvider_Skips()
    {
        var validator = CreateValidator(KeyWrapperNames.Local, KeyWrapperNames.AwsKms);

        var result = validator.Validate(
            null,
            new AzureKeyVaultKeyWrapperOptions { ReferencedKeyHostSuffixes = [] }
        );

        Assert.True(result.Skipped);
    }

    [Fact]
    public void Validate_CustomSuffixes_Succeeds()
    {
        var validator = CreateValidator(KeyWrapperNames.AzureKeyVault);

        var result = validator.Validate(
            null,
            new AzureKeyVaultKeyWrapperOptions
            {
                KeyId = KeyId,
                ReferencedKeyHostSuffixes = [".vault.example.net", ".hsm.example.net"],
            }
        );

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_EmptySuffixes_Fails()
    {
        var validator = CreateValidator(KeyWrapperNames.AzureKeyVault);

        var result = validator.Validate(
            null,
            new AzureKeyVaultKeyWrapperOptions { KeyId = KeyId, ReferencedKeyHostSuffixes = [] }
        );

        Assert.True(result.Failed);
        Assert.Contains(SuffixesKey, result.FailureMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("vault.azure.net")]
    public void Validate_BlankOrUndottedSuffix_Fails(string suffix)
    {
        var validator = CreateValidator(KeyWrapperNames.AzureKeyVault);

        var result = validator.Validate(
            null,
            new AzureKeyVaultKeyWrapperOptions
            {
                KeyId = KeyId,
                ReferencedKeyHostSuffixes = [".vault.azure.net", suffix],
            }
        );

        Assert.True(result.Failed);
        Assert.Contains(SuffixesKey, result.FailureMessage);
    }

    [Fact]
    public void Validate_DuplicateSuffix_Fails()
    {
        var validator = CreateValidator(KeyWrapperNames.AzureKeyVault);

        var result = validator.Validate(
            null,
            new AzureKeyVaultKeyWrapperOptions
            {
                KeyId = KeyId,
                ReferencedKeyHostSuffixes = [".vault.azure.net", ".VAULT.azure.net"],
            }
        );

        Assert.True(result.Failed);
        Assert.Contains(SuffixesKey, result.FailureMessage);
    }

    [Fact]
    public void Validate_MissingKeyIdAndBadSuffixes_ReportsBoth()
    {
        var validator = CreateValidator(KeyWrapperNames.AzureKeyVault);

        var result = validator.Validate(
            null,
            new AzureKeyVaultKeyWrapperOptions { KeyId = null, ReferencedKeyHostSuffixes = [] }
        );

        Assert.True(result.Failed);
        Assert.Contains("KeyId", result.FailureMessage);
        Assert.Contains(SuffixesKey, result.FailureMessage);
    }

    #endregion
}
