// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.AzureKeyVault.Options;

/// <summary>
/// Contains the options for the Azure Key Vault key wrapper, bound from the <see cref="DefaultSectionName"/>
/// configuration section.
/// </summary>
// See ADR-1260.
[PublicAPI]
public sealed class AzureKeyVaultKeyWrapperOptions
{
    /// <summary>
    /// Gets the name of the configuration section the options are bound from.
    /// </summary>
    public const string DefaultSectionName = "SecretProtection:RootKeyProviders:AzureKeyVault";

    /// <summary>
    /// Gets or sets the identifier of the RSA key of 3072 bits or more in Key Vault or Managed HSM that wraps master
    /// keys (<c>https://&lt;vault host&gt;/keys/&lt;name&gt;</c>, optionally followed by <c>/&lt;version&gt;</c>).
    /// Without a version, the key's current version wraps new master keys.
    /// </summary>
    public string? KeyId { get; set; }

    /// <summary>
    /// Gets or sets the host suffixes that the host of a key reference that a caller names must end with, compared
    /// without regard to case. The configured root key is not restricted. Each suffix starts with a dot. The default
    /// is the Key Vault and Managed HSM domains of the public, China and US government clouds:
    /// <c>.vault.azure.net</c>, <c>.managedhsm.azure.net</c>, <c>.vault.azure.cn</c>, <c>.managedhsm.azure.cn</c>,
    /// <c>.vault.usgovcloudapi.net</c> and <c>.managedhsm.usgovcloudapi.net</c>.
    /// </summary>
    /// <remarks>
    /// Configuring the list replaces the default suffixes rather than adding to them. The list is validated when the
    /// host starts whenever the Azure Key Vault key wrapper is the root key provider or one that owners may choose.
    /// </remarks>
    public string[] ReferencedKeyHostSuffixes { get; set; } =
    [
        ".vault.azure.net",
        ".managedhsm.azure.net",
        ".vault.azure.cn",
        ".managedhsm.azure.cn",
        ".vault.usgovcloudapi.net",
        ".managedhsm.usgovcloudapi.net",
    ];
}
