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

using Microsoft.Extensions.Options;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.Secrets.Persistence.AzureKeyVault.Options;

/// <summary>
/// Validates the Azure Key Vault options when the host starts, so a missing or malformed key identifier stops the host
/// instead of failing the first time a master key is wrapped. The options are validated only when the Azure Key Vault
/// root key provider is the one selected.
/// </summary>
// See ADR-0260 and ADR-0137 (configuration is validated at startup).
internal class AzureKeyVaultRootKeyOptionsValidator(
    IOptions<SecretProtectionOptions> protectionOptions
) : IValidateOptions<AzureKeyVaultRootKeyOptions>
{
    private IOptions<SecretProtectionOptions> ProtectionOptions { get; } = protectionOptions;

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, AzureKeyVaultRootKeyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (
            !string.Equals(
                ProtectionOptions.Value.RootKeyProvider,
                RootKeyProviderNames.AzureKeyVault,
                StringComparison.Ordinal
            )
        )
        {
            return ValidateOptionsResult.Skip;
        }

        return AzureKeyVaultKeyId.IsKeyId(options.KeyId)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                "SecretProtection:RootKeyProviders:AzureKeyVault:KeyId must be the identifier of a Key Vault or Managed HSM key, such as https://contoso.vault.azure.net/keys/ncode-identity-root."
            );
    }
}
