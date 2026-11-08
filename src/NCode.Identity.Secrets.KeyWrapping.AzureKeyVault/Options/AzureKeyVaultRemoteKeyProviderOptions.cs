// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.KeyWrapping.AzureKeyVault.Options;

/// <summary>
/// Contains the options for the Azure Key Vault remote key provider, bound from the
/// <see cref="DefaultSectionName"/> configuration section.
/// </summary>
// See ADR-1471.
[PublicAPI]
public sealed class AzureKeyVaultRemoteKeyProviderOptions
{
    /// <summary>
    /// Gets the name of the configuration section the options are bound from.
    /// </summary>
    public const string DefaultSectionName = "SecretProtection:RemoteKeyProviders:AzureKeyVault";

    /// <summary>
    /// Gets or sets the URI of the Key Vault or Managed HSM in which the provider creates keys, such as
    /// <c>https://contoso.vault.azure.net</c>.
    /// </summary>
    public string? VaultUri { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the keys that the provider creates are protected by a hardware security
    /// module. The default is <see langword="true"/>.
    /// </summary>
    public bool HardwareProtected { get; set; } = true;
}
