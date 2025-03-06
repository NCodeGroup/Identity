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
/// Provides an implementation of <see cref="IOpenIdRequestValues"/> that wraps an <see cref="IFormCollection"/>.
/// </summary>
[PublicAPI]
public class OpenIdRequestValuesUsingForm(
    IFormCollection form
) : IOpenIdRequestValues
{
    private IFormCollection Form { get; } = form;

    /// <inheritdoc />
    public string SourceType => OpenIdRequestValuesSourceTypes.Form;

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, StringValues>> GetEnumerator() =>
        Form.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() =>
        ((IEnumerable)Form).GetEnumerator();

    /// <inheritdoc />
    public int Count =>
        Form.Count;

    /// <inheritdoc />
    public ICollection<string> Keys =>
        Form.Keys;

    /// <inheritdoc />
    public bool ContainsKey(string key) =>
        Form.ContainsKey(key);

    /// <inheritdoc />
    public bool TryGetValue(string key, out StringValues value) =>
        Form.TryGetValue(key, out value);

    /// <inheritdoc />
    public StringValues this[string key] =>
        Form[key];
}
