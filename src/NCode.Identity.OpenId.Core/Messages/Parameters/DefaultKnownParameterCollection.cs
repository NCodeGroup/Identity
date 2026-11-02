// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace NCode.Identity.OpenId.Messages.Parameters;

/// <summary>
/// Provides a default implementation of the <see cref="IKnownParameterCollection"/> abstraction.
/// </summary>
internal class DefaultKnownParameterCollection(IEnumerable<KnownParameter> knownParameters)
    : IKnownParameterCollection
{
    private FrozenDictionary<string, KnownParameter> KnownParameters { get; } =
        knownParameters.ToFrozenDictionary(x => x.Name, StringComparer.Ordinal);

    /// <inheritdoc />
    public int Count => KnownParameters.Count;

    /// <inheritdoc />
    public bool TryGet(
        string parameterName,
        [MaybeNullWhen(false)] out KnownParameter knownParameter
    ) => KnownParameters.TryGetValue(parameterName, out knownParameter);

    /// <inheritdoc />
    public IEnumerator<KnownParameter> GetEnumerator() =>
        ((IEnumerable<KnownParameter>)KnownParameters.Values).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
