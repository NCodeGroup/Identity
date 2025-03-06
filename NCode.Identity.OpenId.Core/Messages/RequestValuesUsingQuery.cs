#region Copyright Preamble

// Copyright @ 2025 NCode Group
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
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace NCode.Identity.OpenId.Messages;

/// <summary>
/// Provides an implementation of <see cref="IRequestValues"/> that wraps an <see cref="IQueryCollection"/>.
/// </summary>
[PublicAPI]
public class RequestValuesUsingQuery(
    IQueryCollection query
) : IRequestValues
{
    private IQueryCollection Query { get; } = query;

    /// <inheritdoc />
    public string SourceType => RequestValuesSourceTypes.Query;

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, StringValues>> GetEnumerator() =>
        Query.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() =>
        ((IEnumerable)Query).GetEnumerator();

    /// <inheritdoc />
    public int Count =>
        Query.Count;

    /// <inheritdoc />
    public ICollection<string> Keys =>
        Query.Keys;

    /// <inheritdoc />
    public bool ContainsKey(string key) =>
        Query.ContainsKey(key);

    /// <inheritdoc />
    public bool TryGetValue(string key, out StringValues value) =>
        Query.TryGetValue(key, out value);

    /// <inheritdoc />
    public StringValues this[string key] =>
        Query[key];
}
