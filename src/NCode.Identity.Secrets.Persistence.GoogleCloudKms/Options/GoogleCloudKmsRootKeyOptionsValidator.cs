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

using Microsoft.Extensions.Options;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.Secrets.Persistence.GoogleCloudKms.Options;

/// <summary>
/// Validates the Google Cloud Key Management Service options when the host starts, so a missing or malformed crypto key
/// name stops the host instead of failing the first time a master key is wrapped. The options are validated only when
/// the Google Cloud KMS root key provider is the one selected.
/// </summary>
// See ADR-0260 and ADR-0137 (configuration is validated at startup).
internal class GoogleCloudKmsRootKeyOptionsValidator(
    IOptions<SecretProtectionOptions> protectionOptions
) : IValidateOptions<GoogleCloudKmsRootKeyOptions>
{
    private IOptions<SecretProtectionOptions> ProtectionOptions { get; } = protectionOptions;

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, GoogleCloudKmsRootKeyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (
            !string.Equals(
                ProtectionOptions.Value.RootKeyProvider,
                RootKeyProviderNames.GoogleCloudKms,
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
