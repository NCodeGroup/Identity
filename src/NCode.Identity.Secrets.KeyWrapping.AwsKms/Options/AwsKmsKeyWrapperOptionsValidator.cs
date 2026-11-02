// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Identity.Secrets.Persistence.Options;

namespace NCode.Identity.Secrets.KeyWrapping.AwsKms.Options;

/// <summary>
/// Validates the AWS Key Management Service options when the host starts, so a missing or malformed key ARN stops the
/// host instead of failing the first time a master key is wrapped. The options are validated only when the AWS KMS
/// root key provider is the one selected.
/// </summary>
// See ADR-1235 and ADR-1137 (configuration is validated at startup).
internal class AwsKmsKeyWrapperOptionsValidator(IOptions<SecretProtectionOptions> protectionOptions)
    : IValidateOptions<AwsKmsKeyWrapperOptions>
{
    private IOptions<SecretProtectionOptions> ProtectionOptions { get; } = protectionOptions;

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, AwsKmsKeyWrapperOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (
            !string.Equals(
                ProtectionOptions.Value.RootKeyProvider,
                KeyWrapperNames.AwsKms,
                StringComparison.Ordinal
            )
        )
        {
            return ValidateOptionsResult.Skip;
        }

        return AwsKmsKeyArn.IsValid(options.KeyArn)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                "SecretProtection:RootKeyProviders:AwsKms:KeyArn must be the ARN of a KMS key or alias, such as arn:aws:kms:us-west-2:111122223333:alias/ncode-identity-root."
            );
    }
}
