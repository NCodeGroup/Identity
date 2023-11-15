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

using System.ComponentModel;

namespace NIdentity.OpenId;

/// <summary>
/// Specifies how a client application can acquire an access token.
/// </summary>
[TypeConverter(typeof(OpenIdEnumConverter<GrantType>))]
public enum GrantType
{
    /// <summary>
    /// Represents a value that hasn't been initialized yet and its value is unknown.
    /// </summary>
    [Browsable(false)]
    Unspecified = 0,

    /// <summary>
    /// This grant is used by confidential and public clients to exchange an authorization code for an access token.
    /// It is recommended that all clients use the PKCE extension with this grant to provide additional security.
    /// See https://tools.ietf.org/html/rfc6749#section-1.3.1 for more information.
    /// </summary>
    [OpenIdLabel(OpenIdConstants.GrantTypes.AuthorizationCode)]
    AuthorizationCode,

    /// <summary>
    /// This legacy grant was previously intended for public clients or applications which were unable to securely
    /// store client secrets. Public clients should now use the <see cref="AuthorizationCode"/> grant with the PKCE
    /// extension instead.
    /// See https://tools.ietf.org/html/rfc6749#section-1.3.2 for more information.
    /// </summary>
    [OpenIdLabel(OpenIdConstants.GrantTypes.Implicit)]
    Implicit,

    /// <summary>
    /// This grant is a mix of the <see cref="AuthorizationCode"/> grant and the the <see cref="Implicit"/> grant
    /// where the ID token is returned directly from the authorization endpoint and the Access/Refresh tokens are
    /// returned from the token endpoint.
    /// See https://openid.net/specs/openid-connect-core-1_0.html#HybridFlowAuth for more information.
    /// </summary>
    [OpenIdLabel(OpenIdConstants.GrantTypes.Hybrid)]
    Hybrid,

    /// <summary>
    /// This grant exchanges the user's credentials for an access token. Because the client application has to
    /// collect the user's password and send it to the authorization server, it is not recommended that this grant
    /// be used at all anymore.
    /// See https://tools.ietf.org/html/rfc6749#section-1.3.3 for more information.
    /// </summary>
    [OpenIdLabel(OpenIdConstants.GrantTypes.Password)]
    Password,

    /// <summary>
    /// The this grant is used by clients to obtain an access token outside of the context of a user.
    /// See https://tools.ietf.org/html/rfc6749#section-1.3.4 for more information.
    /// </summary>
    [OpenIdLabel(OpenIdConstants.GrantTypes.ClientCredentials)]
    ClientCredentials,

    /// <summary>
    /// This grant is used by clients to exchange a refresh token for a new access token when the previous access
    /// token has expired.
    /// See https://tools.ietf.org/html/rfc6749#section-1.5 for more information.
    /// </summary>
    [OpenIdLabel(OpenIdConstants.GrantTypes.RefreshToken)]
    RefreshToken,

    /// <summary>
    /// This grant is used by devices with no browser or limited input capability to exchange a previously obtained
    /// device code for an access token.
    /// See https://tools.ietf.org/html/rfc8628 for more information.
    /// </summary>
    [OpenIdLabel(OpenIdConstants.GrantTypes.DeviceCode)]
    DeviceCode
}
