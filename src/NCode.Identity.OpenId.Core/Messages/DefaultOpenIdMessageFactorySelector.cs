// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;

namespace NCode.Identity.OpenId.Messages;

/// <summary>
/// Provides a default implementation of the <see cref="IOpenIdMessageFactorySelector"/> abstraction.
/// </summary>
internal class DefaultOpenIdMessageFactorySelector(IEnumerable<IOpenIdMessageFactory> factories)
    : IOpenIdMessageFactorySelector
{
    private FrozenDictionary<string, IOpenIdMessageFactory> Factories { get; } =
        factories.ToFrozenDictionary(x => x.TypeDiscriminator);

    /// <inheritdoc />
    public IOpenIdMessageFactory GetFactory(string typeDiscriminator) =>
        !Factories.TryGetValue(typeDiscriminator, out var factory)
            ? throw new InvalidOperationException(
                $"No message factory found for type discriminator '{typeDiscriminator}'."
            )
            : factory;
}
