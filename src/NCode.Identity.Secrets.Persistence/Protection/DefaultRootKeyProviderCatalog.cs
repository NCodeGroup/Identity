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

using System.Collections.Frozen;
using Microsoft.Extensions.Options;
using NCode.Identity.Secrets.Persistence.Options;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Provides the default implementation of <see cref="IRootKeyProviderCatalog"/> over the registered
/// <see cref="IRootKeyProvider"/> instances. Providers are keyed ordinally by
/// <see cref="IRootKeyProvider.ProviderName"/>.
/// </summary>
/// <remarks>
/// The constructor throws an <see cref="InvalidOperationException"/> when more than one registered provider has the
/// same name, so an inconsistent registration fails when the catalog is composed rather than when a key is wrapped.
/// </remarks>
/// <param name="providers">The registered root key providers.</param>
/// <param name="options">The options that select the provider for wrapping new keys.</param>
// See ADR-0150 and ADR-0228.
internal class DefaultRootKeyProviderCatalog(
    IEnumerable<IRootKeyProvider> providers,
    IOptions<SecretProtectionOptions> options
) : IRootKeyProviderCatalog
{
    private FrozenDictionary<string, IRootKeyProvider> ProvidersByName { get; } =
        IndexByName(providers);

    private IOptions<SecretProtectionOptions> ProtectionOptions { get; } = options;

    /// <inheritdoc />
    public IRootKeyProvider Get(string providerName)
    {
        if (ProvidersByName.TryGetValue(providerName, out var provider))
            return provider;

        throw new InvalidOperationException(
            $"No root key provider is registered with the name '{providerName}'. "
                + $"The registered providers are: {FormatNames(ProvidersByName.Keys)}."
        );
    }

    /// <inheritdoc />
    public IRootKeyProvider GetConfigured()
    {
        var name = ProtectionOptions.Value.RootKeyProvider;
        return Get(string.IsNullOrWhiteSpace(name) ? RootKeyProviderNames.Ephemeral : name);
    }

    private static FrozenDictionary<string, IRootKeyProvider> IndexByName(
        IEnumerable<IRootKeyProvider> providers
    )
    {
        var providersByName = new Dictionary<string, IRootKeyProvider>(StringComparer.Ordinal);
        var duplicateNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var provider in providers)
        {
            var providerName = provider.ProviderName;

            if (!providersByName.TryAdd(providerName, provider))
                duplicateNames.Add(providerName);
        }

        if (duplicateNames.Count > 0)
            throw new InvalidOperationException(
                $"More than one root key provider is registered with the same name: {FormatNames(duplicateNames)}."
            );

        return providersByName.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private static string FormatNames(IEnumerable<string> names) =>
        string.Join(", ", names.Order(StringComparer.Ordinal).Select(name => $"'{name}'"));
}
