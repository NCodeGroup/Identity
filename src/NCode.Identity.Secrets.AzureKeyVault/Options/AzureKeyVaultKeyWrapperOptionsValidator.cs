// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Identity.Secrets.KeyWrapping;
using NCode.Identity.Secrets.Persistence.Options;

namespace NCode.Identity.Secrets.AzureKeyVault.Options;

/// <summary>
/// Validates the Azure Key Vault options when the host starts, so a missing or malformed key identifier stops the host
/// instead of failing the first time a master key is wrapped. The key identifier is validated only when the Azure Key
/// Vault root key provider is the one selected, and the host suffixes of a referenced key are validated as well when
/// owners may choose the provider for their root key.
/// </summary>
// See ADR-1260, ADR-1414 and ADR-1137 (configuration is validated at startup).
internal class AzureKeyVaultKeyWrapperOptionsValidator(
    IOptions<SecretProtectionOptions> protectionOptions
) : IValidateOptions<AzureKeyVaultKeyWrapperOptions>
{
    private const string SuffixesKey =
        "SecretProtection:RootKeyProviders:AzureKeyVault:ReferencedKeyHostSuffixes";

    private IOptions<SecretProtectionOptions> ProtectionOptions { get; } = protectionOptions;

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, AzureKeyVaultKeyWrapperOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var protection = ProtectionOptions.Value;
        var isRoot = string.Equals(
            protection.RootKeyProvider,
            KeyWrapperNames.AzureKeyVault,
            StringComparison.Ordinal
        );
        var isOwnerProvider = protection.OwnerRootKeys.Providers.Contains(
            KeyWrapperNames.AzureKeyVault,
            StringComparer.Ordinal
        );

        if (!isRoot && !isOwnerProvider)
            return ValidateOptionsResult.Skip;

        var failures = new List<string>();

        if (isRoot && !AzureKeyVaultKeyId.IsKeyId(options.KeyId))
        {
            failures.Add(
                "SecretProtection:RootKeyProviders:AzureKeyVault:KeyId must be the identifier of a Key Vault or Managed HSM key, such as https://contoso.vault.azure.net/keys/ncode-identity-root."
            );
        }

        ValidateSuffixes(options.ReferencedKeyHostSuffixes, failures);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateSuffixes(string[]? suffixes, List<string> failures)
    {
        if (suffixes is not { Length: > 0 })
        {
            failures.Add($"{SuffixesKey} must list at least one host suffix.");
            return;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var suffix in suffixes)
        {
            if (string.IsNullOrWhiteSpace(suffix))
            {
                failures.Add($"{SuffixesKey} must not contain a blank entry.");
            }
            else if (!suffix.StartsWith('.'))
            {
                failures.Add($"{SuffixesKey} entry '{suffix}' must start with a dot.");
            }
            else if (!seen.Add(suffix))
            {
                failures.Add($"{SuffixesKey} entry '{suffix}' is listed more than once.");
            }
        }
    }
}
