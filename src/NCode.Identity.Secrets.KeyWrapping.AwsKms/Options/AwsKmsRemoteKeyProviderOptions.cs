// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.KeyWrapping.AwsKms.Options;

/// <summary>
/// Contains the options for the AWS Key Management Service remote key provider, bound from the
/// <see cref="DefaultSectionName"/> configuration section.
/// </summary>
// See ADR-1471.
[PublicAPI]
public sealed class AwsKmsRemoteKeyProviderOptions
{
    /// <summary>
    /// Gets the name of the configuration section the options are bound from.
    /// </summary>
    public const string DefaultSectionName = "SecretProtection:RemoteKeyProviders:AwsKms";

    /// <summary>
    /// Gets or sets the system name of the AWS region in which the provider creates keys, such as
    /// <c>us-west-2</c>.
    /// </summary>
    public string? Region { get; set; }

    /// <summary>
    /// Gets or sets the number of days, from 7 to 30, that AWS KMS waits before it deletes a key that the provider
    /// destroys. The default is 30.
    /// </summary>
    public int PendingWindowInDays { get; set; } = 30;
}
