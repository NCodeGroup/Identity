// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Collections.Providers;
using NCode.Identity.Secrets.Verifiers;

namespace NCode.Identity.Secrets.Persistence.Options;

/// <summary>
/// Validates <see cref="SharedSecretVerifierOptions"/> as the host starts: each scheme it names has a registered
/// <see cref="SecretVerifierSchemeDescriptor"/>. It reads the descriptors, which are data, never the schemes, whose
/// construction reads options of their own.
/// </summary>
// See ADR-1391.
internal sealed class SharedSecretVerifierOptionsValidator(
    IEnumerable<ICollectionDataSource<SecretVerifierSchemeDescriptor>> descriptorDataSources
) : IValidateOptions<SharedSecretVerifierOptions>
{
    private IEnumerable<
        ICollectionDataSource<SecretVerifierSchemeDescriptor>
    > DescriptorDataSources { get; } = descriptorDataSources;

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, SharedSecretVerifierOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var registeredNames = DescriptorDataSources
            .SelectMany(dataSource => dataSource.Collection)
            .Select(descriptor => descriptor.Name)
            .ToHashSet(StringComparer.Ordinal);

        List<string> failures = [];

        AddIfNotRegistered(
            failures,
            registeredNames,
            nameof(SharedSecretVerifierOptions.GeneratedSecretScheme),
            options.GeneratedSecretScheme
        );
        AddIfNotRegistered(
            failures,
            registeredNames,
            nameof(SharedSecretVerifierOptions.SuppliedSecretScheme),
            options.SuppliedSecretScheme
        );

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void AddIfNotRegistered(
        List<string> failures,
        HashSet<string> registeredNames,
        string optionName,
        string? schemeName
    )
    {
        if (string.IsNullOrEmpty(schemeName) || !registeredNames.Contains(schemeName))
        {
            failures.Add(
                $"'{nameof(SharedSecretVerifierOptions)}.{optionName}' is '{schemeName}', which names no registered "
                    + "secret verifier scheme."
            );
        }
    }
}
