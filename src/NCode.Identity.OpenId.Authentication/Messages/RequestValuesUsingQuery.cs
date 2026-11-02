// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace NCode.Identity.OpenId.Authentication.Messages;

/// <summary>
/// Provides an implementation of <see cref="IRequestValues"/> that wraps an <see cref="IQueryCollection"/>.
/// </summary>
internal class RequestValuesUsingQuery(IQueryCollection query) : IRequestValues
{
    private IQueryCollection Query { get; } = query;

    /// <inheritdoc />
    public string SourceType => RequestValuesSourceTypes.Query;

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, StringValues>> GetEnumerator() => Query.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)Query).GetEnumerator();

    /// <inheritdoc />
    public int Count => Query.Count;

    /// <inheritdoc />
    public ICollection<string> Keys => Query.Keys;

    /// <inheritdoc />
    public bool ContainsKey(string key) => Query.ContainsKey(key);

    /// <inheritdoc />
    public bool TryGetValue(string key, out StringValues value) =>
        Query.TryGetValue(key, out value);

    /// <inheritdoc />
    public StringValues this[string key] => Query[key];
}
