// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.KeyWrapping;

/// <summary>
/// Contains the names of the key wrappers that the library ships, as reported by <see cref="IKeyWrapper.Name"/>. The
/// name is an open string, so these are not necessarily all of the key wrappers that exist. A secret protection
/// configuration selects its root key wrapper by one of these names.
/// </summary>
// See ADR-1279.
[PublicAPI]
public static class KeyWrapperNames
{
    /// <summary>
    /// Contains the name of the key wrapper that keeps a random key encryption key in memory for the lifetime of the process.
    /// </summary>
    public const string Ephemeral = "ephemeral";

    /// <summary>
    /// Contains the name of the key wrapper that reads the key encryption key from a local file.
    /// </summary>
    public const string Local = "local";

    /// <summary>
    /// Contains the name of the key wrapper that wraps keys with a key held in AWS Key Management Service.
    /// </summary>
    public const string AwsKms = "aws-kms";

    /// <summary>
    /// Contains the name of the key wrapper that wraps keys with an RSA key held in Azure Key Vault or Managed HSM.
    /// </summary>
    public const string AzureKeyVault = "azure-key-vault";

    /// <summary>
    /// Contains the name of the key wrapper that wraps keys with a symmetric key held in Google Cloud KMS.
    /// </summary>
    public const string GoogleCloudKms = "google-cloud-kms";
}
