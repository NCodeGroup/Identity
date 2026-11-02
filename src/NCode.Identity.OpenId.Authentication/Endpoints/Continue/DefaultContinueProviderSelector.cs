// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Continue;

/// <summary>
/// Provides a default implementation of the <see cref="IContinueProviderSelector"/> abstraction.
/// </summary>
internal class DefaultContinueProviderSelector(IEnumerable<IContinueProvider> providers)
    : IContinueProviderSelector
{
    private FrozenDictionary<string, IContinueProvider> Lookup { get; } =
        providers.ToFrozenDictionary(x => x.ContinueCode, StringComparer.Ordinal);

    /// <inheritdoc />
    public IContinueProvider SelectProvider(string continueCode)
    {
        if (!Lookup.TryGetValue(continueCode, out var provider))
        {
            throw new InvalidOperationException(
                $"No continue provider found with continue code '{continueCode}'."
            );
        }

        return provider;
    }
}
