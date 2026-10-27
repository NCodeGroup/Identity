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

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Client;

/// <summary>
/// Represents a request to the token endpoint of an OpenID provider. The client writes the
/// <c>grant_type</c>, <c>scope</c> and <c>resource</c> parameters; a subclass writes only its own parameters.
/// </summary>
[PublicAPI]
public abstract class TokenRequest
{
    /// <summary>
    /// Gets the value of the <c>grant_type</c> parameter.
    /// </summary>
    public abstract string GrantType { get; }

    /// <summary>
    /// Gets the optional space-delimited <c>scope</c> parameter.
    /// </summary>
    public string? Scope { get; init; }

    /// <summary>
    /// Gets the <c>resource</c> indicators (RFC 8707), each written as its own parameter.
    /// </summary>
    public List<string> Resources { get; init; } = [];

    /// <summary>
    /// Adds the parameters that are specific to this grant type.
    /// </summary>
    /// <param name="parameters">The collection to add the parameters to.</param>
    public abstract void AddGrantParameters(ICollection<KeyValuePair<string, string>> parameters);
}
