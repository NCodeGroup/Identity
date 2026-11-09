// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace NCode.Identity.Secrets.AzureKeyVault.Options;

/// <summary>
/// Validates the Azure Key Vault remote key provider options when the host starts, so a malformed vault URI stops the
/// host instead of failing the first time a key is created. A provider without a vault URI registers, and creates and
/// lists no keys.
/// </summary>
// See ADR-1471 and ADR-1137 (configuration is validated at startup).
internal sealed partial class AzureKeyVaultRemoteKeyProviderOptionsValidator
    : IValidateOptions<AzureKeyVaultRemoteKeyProviderOptions>
{
    // '\z' rather than '$', which also matches before a trailing newline; the pattern accepts ASCII letters and digits
    // only.
    [GeneratedRegex(@"^https://[A-Za-z0-9-]+(\.[A-Za-z0-9-]+)+/?\z")]
    private static partial Regex VaultUriPattern();

    /// <inheritdoc />
    public ValidateOptionsResult Validate(
        string? name,
        AzureKeyVaultRemoteKeyProviderOptions options
    )
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrEmpty(options.VaultUri) || VaultUriPattern().IsMatch(options.VaultUri))
            return ValidateOptionsResult.Success;

        return ValidateOptionsResult.Fail(
            "SecretProtection:RemoteKeyProviders:AzureKeyVault:VaultUri must be the HTTPS URI of a Key Vault or Managed HSM, such as https://contoso.vault.azure.net."
        );
    }
}
