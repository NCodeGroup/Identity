// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.RemoteKeys;

/// <summary>
/// Contains the names of the remote key providers that the library ships. The name is an open string, so these are
/// not necessarily all of the providers that exist. Each name is the name of the key wrapper for the same key
/// management service, so one name selects one service.
/// </summary>
// See ADR-1471.
[PublicAPI]
public static class RemoteKeyProviderNames
{
    /// <summary>
    /// Contains the name of the provider for keys held in AWS Key Management Service.
    /// </summary>
    public const string AwsKms = "aws-kms";

    /// <summary>
    /// Contains the name of the provider for keys held in Azure Key Vault or Managed HSM.
    /// </summary>
    public const string AzureKeyVault = "azure-key-vault";

    /// <summary>
    /// Contains the name of the provider for keys held in Google Cloud KMS.
    /// </summary>
    public const string GoogleCloudKms = "google-cloud-kms";
}
