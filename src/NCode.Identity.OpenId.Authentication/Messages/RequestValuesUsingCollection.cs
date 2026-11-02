// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using Microsoft.Extensions.Primitives;

namespace NCode.Identity.OpenId.Authentication.Messages;

/// <summary>
/// Provides an implementation of <see cref="IRequestValues"/> backed by an in-memory dictionary. Used to replay the
/// parameters of a pushed authorization request (RFC 9126) at the authorization endpoint.
/// </summary>
internal sealed class RequestValuesUsingCollection(
    string sourceType,
    IReadOnlyDictionary<string, StringValues> values
) : IRequestValues
{
    private IReadOnlyDictionary<string, StringValues> Values { get; } = values;

    /// <inheritdoc />
    public string SourceType { get; } = sourceType;

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, StringValues>> GetEnumerator() =>
        Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)Values).GetEnumerator();

    /// <inheritdoc />
    public int Count => Values.Count;

    /// <inheritdoc />
    public ICollection<string> Keys => Values.Keys.ToList();

    /// <inheritdoc />
    public bool ContainsKey(string key) => Values.ContainsKey(key);

    /// <inheritdoc />
    public bool TryGetValue(string key, out StringValues value) =>
        Values.TryGetValue(key, out value);

    /// <inheritdoc />
    public StringValues this[string key] =>
        Values.TryGetValue(key, out var value) ? value : StringValues.Empty;
}
