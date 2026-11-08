// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Identity.Secrets.KeyWrapping;
using NCode.Identity.Secrets.Persistence.Options;

namespace NCode.Identity.Secrets.GoogleCloudKms.Options;

/// <summary>
/// Validates the Google Cloud Key Management Service options when the host starts, so a missing or malformed crypto key
/// name stops the host instead of failing the first time a master key is wrapped. The options are validated only when
/// the Google Cloud KMS key wrapper is the one selected.
/// </summary>
// See ADR-1260 and ADR-1137 (configuration is validated at startup).
internal class GoogleCloudKmsKeyWrapperOptionsValidator(
    IOptions<SecretProtectionOptions> protectionOptions
) : IValidateOptions<GoogleCloudKmsKeyWrapperOptions>
{
    private IOptions<SecretProtectionOptions> ProtectionOptions { get; } = protectionOptions;

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, GoogleCloudKmsKeyWrapperOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (
            !string.Equals(
                ProtectionOptions.Value.RootKeyProvider,
                KeyWrapperNames.GoogleCloudKms,
                StringComparison.Ordinal
            )
        )
        {
            return ValidateOptionsResult.Skip;
        }

        return GoogleCloudKmsKeyName.IsCryptoKeyName(options.KeyName)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                "SecretProtection:RootKeyProviders:GoogleCloudKms:KeyName must be the resource name of a Cloud KMS crypto key, such as projects/my-project/locations/us-central1/keyRings/ncode-identity/cryptoKeys/root."
            );
    }
}
