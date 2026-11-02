// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Secrets.KeyWrapping;

namespace NCode.Identity.Secrets.Persistence.Options;

/// <summary>
/// Contains the options that configure how stored secrets are protected, bound from the
/// <see cref="DefaultSectionName"/> configuration section. Each root key provider binds its own options from a child
/// of the <c>RootKeyProviders</c> section, so the options a provider needs are not part of this type.
/// </summary>
// See ADR-1235.
[PublicAPI]
public sealed class SecretProtectionOptions
{
    /// <summary>
    /// Gets the name of the configuration section the options are bound from.
    /// </summary>
    public const string DefaultSectionName = "SecretProtection";

    /// <summary>
    /// Gets or sets the name of the root key provider that protects the master keys, for example
    /// <see cref="KeyWrapperNames.Ephemeral"/>, <see cref="KeyWrapperNames.Local"/>,
    /// <see cref="KeyWrapperNames.AwsKms"/>, <see cref="KeyWrapperNames.AzureKeyVault"/>,
    /// <see cref="KeyWrapperNames.GoogleCloudKms"/>, or the name of any other registered provider. When unset or
    /// blank, the ephemeral provider is used: it keeps a random key in memory, so every restart makes the stored
    /// secrets unreadable, and Production refuses it.
    /// </summary>
    public string? RootKeyProvider { get; set; }

    /// <summary>
    /// Gets or sets the options for the offline recovery key that every master key is additionally wrapped under.
    /// </summary>
    public RecoveryKeyOptions Recovery { get; set; } = new();
}
