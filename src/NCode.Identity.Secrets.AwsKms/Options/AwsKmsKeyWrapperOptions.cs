// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.AwsKms.Options;

/// <summary>
/// Contains the options for the AWS Key Management Service key wrapper, bound from the
/// <see cref="DefaultSectionName"/> configuration section.
/// </summary>
// See ADR-1235.
[PublicAPI]
public sealed class AwsKmsKeyWrapperOptions
{
    /// <summary>
    /// Gets the name of the configuration section the options are bound from.
    /// </summary>
    public const string DefaultSectionName = "SecretProtection:RootKeyProviders:AwsKms";

    /// <summary>
    /// Gets or sets the ARN of the symmetric encryption KMS key that wraps master keys, or of an alias that names one
    /// (<c>arn:aws:kms:&lt;region&gt;:&lt;account&gt;:key/&lt;id&gt;</c> or
    /// <c>arn:aws:kms:&lt;region&gt;:&lt;account&gt;:alias/&lt;name&gt;</c>). The region in the ARN selects the KMS
    /// endpoint.
    /// </summary>
    public string? KeyArn { get; set; }
}
