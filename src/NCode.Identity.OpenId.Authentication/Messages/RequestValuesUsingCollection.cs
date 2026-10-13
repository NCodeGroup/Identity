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
