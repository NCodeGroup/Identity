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

using System.Text.Json;
using System.Text.Json.Serialization;
using NCode.Identity.Claims;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Serialization;

/// <summary>
/// Provides a default implementation of <see cref="IOpenIdJsonConverterProvider"/> that returns a set of JSON converters
/// suitable for OpenID messages and related types.
/// This implementation includes converters for URIs, string values, enums, OpenID messages, claims, and claims identities.
/// It uses a provided <see cref="IClaimsSerializer"/> to handle serialization and deserialization of claims and claims identities.
/// The converters are returned in a collection that can be used to configure JSON serialization options in an OpenID environment.
/// </summary>
public class DefaultOpenIdJsonConverterProvider(IClaimsSerializer claimsSerializer) : IOpenIdJsonConverterProvider
{
    private IClaimsSerializer ClaimsSerializer { get; } = claimsSerializer;

    /// <inheritdoc />
    public IEnumerable<JsonConverter> GetJsonConverters(OpenIdEnvironment openIdEnvironment)
    {
        return
        [
            new UriJsonConverter(),
            new StringValuesJsonConverter(),
            new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower),
            new OpenIdMessageJsonConverterFactory(openIdEnvironment),
            // TODO
            // new AuthorizationRequestJsonConverter(),
            // new DelegatingJsonConverter<IRequestClaim, RequestClaim>(),
            // new DelegatingJsonConverter<IRequestClaims, RequestClaims>(),
            new ClaimJsonConverter(ClaimsSerializer),
            new ClaimsIdentityJsonConverter(ClaimsSerializer),
            new ClaimsPrincipalJsonConverter(ClaimsSerializer)
        ];
    }
}
