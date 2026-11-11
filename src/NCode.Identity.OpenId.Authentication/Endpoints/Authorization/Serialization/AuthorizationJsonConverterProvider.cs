// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;
using NCode.Identity.Json;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Serialization;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Serialization;

/// <summary>
/// Provides <see cref="JsonConverter"/> instances for authorization-related types used during
/// the OAuth 2.0 / OpenID Connect authorization endpoint processing.
/// </summary>
internal class AuthorizationJsonConverterProvider : IOpenIdJsonConverterProvider
{
    /// <inheritdoc />
    public IEnumerable<JsonConverter> GetJsonConverters(OpenIdEnvironment openIdEnvironment)
    {
        return
        [
            new AuthorizationRequestJsonConverter(),
            new DelegatingJsonConverter<IRequestClaim, RequestClaim>(),
            new DelegatingJsonConverter<IRequestClaims, RequestClaims>(),
        ];
    }
}
