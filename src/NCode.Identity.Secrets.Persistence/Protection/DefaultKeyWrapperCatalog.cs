// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;
using NCode.Identity.Secrets.KeyWrapping;
using NCode.Identity.Secrets.Persistence.Options;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Provides the default implementation of <see cref="IKeyWrapperCatalog"/> over the registered
/// <see cref="IKeyWrapper"/> instances. Providers are keyed ordinally by
/// <see cref="IKeyWrapper.Name"/>.
/// </summary>
/// <remarks>
/// The constructor throws an <see cref="InvalidOperationException"/> when more than one registered provider has the
/// same name, and when a provider that <see cref="OwnerRootKeyOptions.Providers"/> lists is not a registered
/// <see cref="IReferencedKeyWrapper"/>, so an inconsistent registration or configuration fails when the catalog is
/// composed rather than when a key is wrapped.
/// </remarks>
// See ADR-1150, ADR-1235, ADR-1414.
internal class DefaultKeyWrapperCatalog : IKeyWrapperCatalog
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultKeyWrapperCatalog"/> class.
    /// </summary>
    /// <param name="providers">The registered root key providers.</param>
    /// <param name="options">The options that select the provider for wrapping new keys.</param>
    public DefaultKeyWrapperCatalog(
        IEnumerable<IKeyWrapper> providers,
        IOptions<SecretProtectionOptions> options
    )
    {
        // The registrations are enumerated once, so a lazy sequence is not resolved twice.
        var buffered = providers.ToArray();

        ProvidersByName = IndexByName(buffered);
        ReferencedByName = buffered
            .OfType<IReferencedKeyWrapper>()
            .ToFrozenDictionary(wrapper => wrapper.Name, StringComparer.Ordinal);
        ProtectionOptions = options;

        ValidateOwnerRootKeyProviders(options.Value.OwnerRootKeys.Providers);
    }

    private FrozenDictionary<string, IKeyWrapper> ProvidersByName { get; }

    private FrozenDictionary<string, IReferencedKeyWrapper> ReferencedByName { get; }

    private IOptions<SecretProtectionOptions> ProtectionOptions { get; }

    /// <inheritdoc />
    public IKeyWrapper Get(string providerName)
    {
        if (ProvidersByName.TryGetValue(providerName, out var provider))
            return provider;

        throw new InvalidOperationException(
            $"No root key provider is registered with the name '{providerName}'. "
                + $"The registered providers are: {FormatNames(ProvidersByName.Keys)}."
        );
    }

    /// <inheritdoc />
    public bool TryGetReferenced(
        string name,
        [NotNullWhen(true)] out IReferencedKeyWrapper? wrapper
    ) => ReferencedByName.TryGetValue(name, out wrapper);

    /// <inheritdoc />
    public IReferencedKeyWrapper GetReferenced(string name)
    {
        if (ReferencedByName.TryGetValue(name, out var wrapper))
            return wrapper;

        throw new InvalidOperationException(
            $"No key wrapper that wraps under a key the caller names is registered with the name '{name}'. "
                + $"The registered ones are: {FormatNames(ReferencedByName.Keys)}."
        );
    }

    /// <inheritdoc />
    public IKeyWrapper GetConfigured()
    {
        var name = ProtectionOptions.Value.RootKeyProvider;

        // Worded like the host's start-up check of the same key, but naming the providers actually registered, which
        // that check cannot resolve.
        if (string.IsNullOrWhiteSpace(name))
        {
            var actual = name is null ? "it is not set" : $"it is blank ('{name}')";

            throw new InvalidOperationException(
                $"{SecretProtectionOptions.DefaultSectionName}:{nameof(SecretProtectionOptions.RootKeyProvider)} is "
                    + $"required, but {actual}. Name one of the registered root key providers: "
                    + $"{FormatNames(ProvidersByName.Keys)}."
            );
        }

        return Get(name);
    }

    private static FrozenDictionary<string, IKeyWrapper> IndexByName(
        IEnumerable<IKeyWrapper> providers
    )
    {
        var providersByName = new Dictionary<string, IKeyWrapper>(StringComparer.Ordinal);
        var duplicateNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var provider in providers)
        {
            var providerName = provider.Name;

            if (!providersByName.TryAdd(providerName, provider))
                duplicateNames.Add(providerName);
        }

        if (duplicateNames.Count > 0)
            throw new InvalidOperationException(
                $"More than one root key provider is registered with the same name: {FormatNames(duplicateNames)}."
            );

        return providersByName.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private void ValidateOwnerRootKeyProviders(string[]? ownerProviders)
    {
        if (ownerProviders is null || ownerProviders.Length == 0)
            return;

        var key =
            $"{SecretProtectionOptions.DefaultSectionName}:{nameof(SecretProtectionOptions.OwnerRootKeys)}:{nameof(OwnerRootKeyOptions.Providers)}";
        var choices = FormatChoices(ReferencedByName.Keys);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var failures = new List<string>();

        foreach (var provider in ownerProviders)
        {
            // Blank and repeated names are reported by the options validator.
            if (string.IsNullOrWhiteSpace(provider) || !seen.Add(provider))
                continue;

            if (ReferencedByName.ContainsKey(provider))
                continue;

            var reason = ProvidersByName.ContainsKey(provider)
                ? "is registered but does not wrap under a key the caller names"
                : "is not registered";

            failures.Add(
                $"{key} names '{provider}', which {reason}. The key wrappers that an owner can choose are: {choices}."
            );
        }

        if (failures.Count > 0)
            throw new InvalidOperationException(string.Join(" ", failures));
    }

    private static string FormatChoices(IEnumerable<string> names) =>
        names.Any() ? FormatNames(names) : "none";

    private static string FormatNames(IEnumerable<string> names) =>
        string.Join(", ", names.Order(StringComparer.Ordinal).Select(name => $"'{name}'"));
}
