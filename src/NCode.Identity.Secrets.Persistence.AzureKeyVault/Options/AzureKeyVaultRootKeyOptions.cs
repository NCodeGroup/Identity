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

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence.AzureKeyVault.Options;

/// <summary>
/// Contains the options for the Azure Key Vault root key provider, bound from the <see cref="DefaultSectionName"/>
/// configuration section.
/// </summary>
// See ADR-0238.
[PublicAPI]
public sealed class AzureKeyVaultRootKeyOptions
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
