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

using System.Security.Claims;
using System.Text.Json.Nodes;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;

namespace NCode.Identity.Bff;

/// <summary>
/// The context in which the <see cref="IBffUserContributor"/> strategies add fields to the <c>/bff/user</c> response.
/// A field may not use one of the <see cref="ReservedFieldNames"/>, which the BFF writes itself, nor a name another
/// contributor already used.
/// </summary>
// See ADR-0290.
[PublicAPI]
public sealed class BffUserContext
{
    private readonly OrderedDictionary<string, JsonNode?> _fields = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the names of the fields the BFF writes itself: <c>sub</c>, <c>name</c>, <c>claims</c>, <c>session</c> and
    /// <c>logoutUrl</c>.
    /// </summary>
    public static IReadOnlySet<string> ReservedFieldNames { get; } =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "sub",
            "name",
            "claims",
            "session",
            "logoutUrl",
        };

    /// <summary>
    /// Initializes a new instance of the <see cref="BffUserContext"/> class.
    /// </summary>
    /// <param name="httpContext">The HTTP context of the request.</param>
    /// <param name="principal">The principal of the session.</param>
    /// <param name="metadata">The metadata of the session.</param>
    public BffUserContext(
        HttpContext httpContext,
        ClaimsPrincipal principal,
        BffSessionMetadata metadata
    )
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(metadata);

        HttpContext = httpContext;
        Principal = principal;
        Metadata = metadata;
    }

    /// <summary>
    /// Gets the HTTP context of the request.
    /// </summary>
    public HttpContext HttpContext { get; }

    /// <summary>
    /// Gets the principal of the session.
    /// </summary>
    public ClaimsPrincipal Principal { get; }

    /// <summary>
    /// Gets the metadata of the session.
    /// </summary>
    public BffSessionMetadata Metadata { get; }

    /// <summary>
    /// Gets the fields the contributors added, in the order they were added.
    /// </summary>
    public IReadOnlyDictionary<string, JsonNode?> Fields => _fields;

    /// <summary>
    /// Adds a field to the response.
    /// </summary>
    /// <param name="name">The name of the field.</param>
    /// <param name="value">The value of the field, or <see langword="null"/> to write a JSON <c>null</c>.</param>
    /// <exception cref="ArgumentException">The name is empty, one of the <see cref="ReservedFieldNames"/>, or already
    /// added.</exception>
    public void AddField(string name, JsonNode? value)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (ReservedFieldNames.Contains(name))
            throw new ArgumentException(
                $"The field '{name}' is reserved by the BFF.",
                nameof(name)
            );

        if (!_fields.TryAdd(name, value))
            throw new ArgumentException($"The field '{name}' was already added.", nameof(name));
    }
}
