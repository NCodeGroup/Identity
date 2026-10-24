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

using NCode.Identity.Secrets.Persistence.AzureKeyVault.Options;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Secrets.Persistence.Protection;
using Xunit;

namespace NCode.Identity.Secrets.Persistence.AzureKeyVault.Tests.Options;

public class AzureKeyVaultRootKeyOptionsValidatorTests
{
    private const string KeyId = "https://contoso.vault.azure.net/keys/ncode-identity-root";

    private const string SecretId = "https://contoso.vault.azure.net/secrets/ncode-identity-root";

    private static AzureKeyVaultRootKeyOptionsValidator CreateValidator(string? rootKeyProvider) =>
        new(
            Microsoft.Extensions.Options.Options.Create(
                new SecretProtectionOptions { RootKeyProvider = rootKeyProvider }
            )
        );

    #region Validate Tests

    [Fact]
    public void Validate_OtherProviderSelected_Skips()
    {
        var validator = CreateValidator(RootKeyProviderNames.Local);

        var result = validator.Validate(null, new AzureKeyVaultRootKeyOptions { KeyId = null });

        Assert.True(result.Skipped);
    }

    [Fact]
    public void Validate_MissingKeyId_Fails()
    {
        var validator = CreateValidator(RootKeyProviderNames.AzureKeyVault);

        var result = validator.Validate(null, new AzureKeyVaultRootKeyOptions { KeyId = null });

        Assert.True(result.Failed);
        Assert.Contains("KeyId", result.FailureMessage);
    }

    [Fact]
    public void Validate_SecretId_Fails()
    {
        var validator = CreateValidator(RootKeyProviderNames.AzureKeyVault);

        var result = validator.Validate(null, new AzureKeyVaultRootKeyOptions { KeyId = SecretId });

        Assert.True(result.Failed);
        Assert.Contains("KeyId", result.FailureMessage);
    }

    [Fact]
    public void Validate_KeyId_Succeeds()
    {
        var validator = CreateValidator(RootKeyProviderNames.AzureKeyVault);

        var result = validator.Validate(null, new AzureKeyVaultRootKeyOptions { KeyId = KeyId });

        Assert.True(result.Succeeded);
    }

    #endregion
}
