// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Identity.Secrets.KeyWrapping;
using NCode.Identity.Secrets.Persistence.Options;

namespace NCode.Identity.Server.SecretProtection;

/// <summary>
/// Validates <see cref="SecretProtectionOptions"/> when the host starts: it must name the root key provider that
/// protects the stored secrets, because no provider is selected by default.
/// </summary>
// See ADR-1137 (fail at startup) and ADR-1370 (no root key provider by default).
internal sealed class SecretProtectionOptionsValidator : IValidateOptions<SecretProtectionOptions>
{
    /// <summary>
    /// The configuration key that names the root key provider, which every failure message names.
    /// </summary>
    internal const string RootKeyProviderKey =
        $"{SecretProtectionOptions.DefaultSectionName}:{nameof(SecretProtectionOptions.RootKeyProvider)}";

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, SecretProtectionOptions options)
    {
        var rootKeyProvider = options.RootKeyProvider;
        if (!string.IsNullOrWhiteSpace(rootKeyProvider))
            return ValidateOptionsResult.Success;

        var actual = rootKeyProvider is null
            ? "it is not set"
            : $"it is blank ('{rootKeyProvider}')";

        return ValidateOptionsResult.Fail(
            $"{RootKeyProviderKey} is required, but {actual}. Name the root key provider that protects the stored "
                + $"secrets, such as '{KeyWrapperNames.Local}', '{KeyWrapperNames.AwsKms}', "
                + $"'{KeyWrapperNames.AzureKeyVault}' or '{KeyWrapperNames.GoogleCloudKms}'; local development and "
                + $"tests may select '{KeyWrapperNames.Ephemeral}', whose root key is lost when the process stops."
        );
    }
}
