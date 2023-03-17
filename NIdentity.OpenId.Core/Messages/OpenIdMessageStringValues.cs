#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

namespace NIdentity.OpenId.Messages;

internal class OpenIdMessageStringValues : IOpenIdMessage
{
    public IOpenIdContext OpenIdContext { get; }

    private IReadOnlyDictionary<string, StringValues> Properties { get; }

    public OpenIdMessageStringValues(IOpenIdContext context, IEnumerable<KeyValuePair<string, StringValues>> properties)
    {
        OpenIdContext = context;
        Properties = properties.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public int Count =>
        Properties.Count;

    /// <inheritdoc />
    public IEnumerable<string> Keys =>
        Properties.Keys;

    /// <inheritdoc />
    public IEnumerable<StringValues> Values =>
        Properties.Values;

    /// <inheritdoc />
    public StringValues this[string key] =>
        Properties.TryGetValue(key, out var value) ? value : StringValues.Empty;

    /// <inheritdoc />
    public bool ContainsKey(string key) =>
        Properties.ContainsKey(key);

    /// <inheritdoc />
    public bool TryGetValue(string key, out StringValues value) =>
        Properties.TryGetValue(key, out value);

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, StringValues>> GetEnumerator() =>
        Properties.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() =>
        ((IEnumerable)Properties).GetEnumerator();
}
