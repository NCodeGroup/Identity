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

using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Jwks.Results;

/// <summary>
/// Contains the parameters for a <c>JSON Web Key Set (JWKS)</c> response as defined by
/// <c>RFC 7517</c>.
/// </summary>
/// <remarks>
/// This type is <b>serialize-only</b> (one-way): it is intended to produce (write) JWKS responses.
/// Its <see cref="JsonWebKey"/> members use discriminator-less polymorphic serialization and therefore
/// cannot be polymorphically deserialized. See <see cref="JsonWebKey"/> for details.
/// </remarks>
/// <seealso href="https://datatracker.ietf.org/doc/html/rfc7517#section-5">RFC 7517 Section 5</seealso>
[PublicAPI]
public class JsonWebKeySetResult
{
    /// <summary>
    /// Gets or sets the collection of <see cref="JsonWebKey"/> instances that make up the key set.
    /// </summary>
    [JsonPropertyName("keys")]
    public IReadOnlyCollection<JsonWebKey> Keys { get; init; } = [];
}
