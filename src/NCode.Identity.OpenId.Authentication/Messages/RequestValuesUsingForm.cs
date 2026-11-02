// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace NCode.Identity.OpenId.Authentication.Messages;

/// <summary>
/// Provides an implementation of <see cref="IRequestValues"/> that wraps an <see cref="IFormCollection"/>.
/// </summary>
internal class RequestValuesUsingForm(IFormCollection form) : IRequestValues
{
    private IFormCollection Form { get; } = form;

    /// <inheritdoc />
    public string SourceType => RequestValuesSourceTypes.Form;

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, StringValues>> GetEnumerator() => Form.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)Form).GetEnumerator();

    /// <inheritdoc />
    public int Count => Form.Count;

    /// <inheritdoc />
    public ICollection<string> Keys => Form.Keys;

    /// <inheritdoc />
    public bool ContainsKey(string key) => Form.ContainsKey(key);

    /// <inheritdoc />
    public bool TryGetValue(string key, out StringValues value) => Form.TryGetValue(key, out value);

    /// <inheritdoc />
    public StringValues this[string key] => Form[key];
}
