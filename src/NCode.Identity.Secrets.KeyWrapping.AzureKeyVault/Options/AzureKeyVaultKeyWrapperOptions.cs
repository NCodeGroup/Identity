// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.KeyWrapping.AzureKeyVault.Options;

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
}
