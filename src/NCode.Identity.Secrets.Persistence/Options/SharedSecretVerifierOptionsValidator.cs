// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Identity.Secrets.Verifiers;

namespace NCode.Identity.Secrets.Persistence.Options;

/// <summary>
/// Validates <see cref="SharedSecretVerifierOptions"/> as the host starts: each scheme it names is registered.
/// </summary>
// See ADR-1391.
internal sealed class SharedSecretVerifierOptionsValidator(
    ISecretVerifierSchemeCatalog secretVerifierSchemeCatalog
) : IValidateOptions<SharedSecretVerifierOptions>
{
    private ISecretVerifierSchemeCatalog SecretVerifierSchemeCatalog { get; } =
        secretVerifierSchemeCatalog;

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, SharedSecretVerifierOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];

        AddIfNotRegistered(
            failures,
            nameof(SharedSecretVerifierOptions.GeneratedSecretScheme),
            options.GeneratedSecretScheme
        );
        AddIfNotRegistered(
            failures,
            nameof(SharedSecretVerifierOptions.SuppliedSecretScheme),
            options.SuppliedSecretScheme
        );

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private void AddIfNotRegistered(List<string> failures, string optionName, string? schemeName)
    {
        if (
            string.IsNullOrEmpty(schemeName)
            || !SecretVerifierSchemeCatalog.TryGet(schemeName, out _)
        )
        {
            failures.Add(
                $"'{nameof(SharedSecretVerifierOptions)}.{optionName}' is '{schemeName}', which names no registered "
                    + "secret verifier scheme."
            );
        }
    }
}
