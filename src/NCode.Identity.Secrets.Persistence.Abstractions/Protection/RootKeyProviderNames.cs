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

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Contains the known values for <see cref="IRootKeyProvider.ProviderName"/>. The provider name is an open string,
/// so these are the root key providers that are known to the library and not necessarily all of the providers that
/// exist.
/// </summary>
// See ADR-0235.
[PublicAPI]
public static class RootKeyProviderNames
{
    /// <summary>
    /// Contains the name of the provider that keeps a random root key in memory for the lifetime of the process.
    /// </summary>
    public const string Ephemeral = "ephemeral";

    /// <summary>
    /// Contains the name of the provider that reads the root key from a local file.
    /// </summary>
    public const string Local = "local";

    /// <summary>
    /// Contains the name of the provider that wraps keys with a key held in AWS Key Management Service.
    /// </summary>
    public const string AwsKms = "aws-kms";

    /// <summary>
    /// Contains the name of the provider that wraps keys with an RSA key held in Azure Key Vault or Managed HSM.
    /// </summary>
    public const string AzureKeyVault = "azure-key-vault";

    /// <summary>
    /// Contains the name of the provider that wraps keys with a symmetric key held in Google Cloud KMS.
    /// </summary>
    public const string GoogleCloudKms = "google-cloud-kms";
}
