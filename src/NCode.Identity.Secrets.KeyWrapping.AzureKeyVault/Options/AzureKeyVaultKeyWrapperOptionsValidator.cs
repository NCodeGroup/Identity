// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Identity.Secrets.Persistence.Options;

namespace NCode.Identity.Secrets.KeyWrapping.AzureKeyVault.Options;

/// <summary>
/// Validates the Azure Key Vault options when the host starts, so a missing or malformed key identifier stops the host
/// instead of failing the first time a master key is wrapped. The options are validated only when the Azure Key Vault
/// root key provider is the one selected.
/// </summary>
// See ADR-1260 and ADR-1137 (configuration is validated at startup).
internal class AzureKeyVaultKeyWrapperOptionsValidator(
    IOptions<SecretProtectionOptions> protectionOptions
) : IValidateOptions<AzureKeyVaultKeyWrapperOptions>
{
    private IOptions<SecretProtectionOptions> ProtectionOptions { get; } = protectionOptions;

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, AzureKeyVaultKeyWrapperOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (
            !string.Equals(
                ProtectionOptions.Value.RootKeyProvider,
                KeyWrapperNames.AzureKeyVault,
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
