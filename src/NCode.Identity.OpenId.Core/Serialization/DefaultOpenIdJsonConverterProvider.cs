// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;
using NCode.Identity.Claims;
using NCode.Identity.Json;
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
internal class DefaultOpenIdJsonConverterProvider(IClaimsSerializer claimsSerializer)
    : IOpenIdJsonConverterProvider
{
    private IClaimsSerializer ClaimsSerializer { get; } = claimsSerializer;

    /// <inheritdoc />
    public IEnumerable<JsonConverter> GetJsonConverters(OpenIdEnvironment openIdEnvironment)
    {
        return
        [
            UriJsonConverter.Singleton,
            new StringValuesJsonConverter(),
            new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower),
            new OpenIdMessageJsonConverterFactory(openIdEnvironment),
            new ClaimJsonConverter(ClaimsSerializer),
            new ClaimsIdentityJsonConverter(ClaimsSerializer),
            new ClaimsPrincipalJsonConverter(ClaimsSerializer),
        ];
    }
}
