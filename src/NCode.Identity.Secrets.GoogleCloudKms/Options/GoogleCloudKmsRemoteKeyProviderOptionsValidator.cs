// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;

namespace NCode.Identity.Secrets.GoogleCloudKms.Options;

/// <summary>
/// Validates the Google Cloud Key Management Service remote key provider options when the host starts, so a malformed
/// key ring name stops the host instead of failing the first time a key is created. A provider without a key ring
/// registers, and creates and lists no keys.
/// </summary>
// See ADR-1471 and ADR-1137 (configuration is validated at startup).
internal sealed class GoogleCloudKmsRemoteKeyProviderOptionsValidator
    : IValidateOptions<GoogleCloudKmsRemoteKeyProviderOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(
        string? name,
        GoogleCloudKmsRemoteKeyProviderOptions options
    )
    {
        ArgumentNullException.ThrowIfNull(options);

        return
            string.IsNullOrEmpty(options.KeyRing)
            || GoogleCloudKmsKeyName.IsKeyRingName(options.KeyRing)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                "SecretProtection:RemoteKeyProviders:GoogleCloudKms:KeyRing must be the resource name of a Cloud KMS key ring, such as projects/my-project/locations/us-central1/keyRings/ncode-identity."
            );
    }
}
