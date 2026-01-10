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

using JetBrains.Annotations;
using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Errors;

/// <summary>
/// Provides extension methods for <see cref="IOpenIdErrorFactory"/> that can be used to create specific
/// <c>OAuth</c> or <c>OpenID Connect</c> errors.
/// </summary>
[PublicAPI]
public static class OpenIdErrorFactoryExtensions
{
    /// <param name="factory">The <see cref="IOpenIdErrorFactory"/> instance.</param>
    extension(IOpenIdErrorFactory factory)
    {
        /// <summary>
        /// Creates an <see cref="IOpenIdError"/> for when processing an <c>OAuth</c> or <c>OpenID Connect</c> message contains invalid client credentials.
        /// </summary>
        /// <returns>The newly created <see cref="IOpenIdError"/> instance.</returns>
        public IOpenIdError InvalidClient()
        {
            return factory.Create(OpenIdConstants.ErrorCodes.InvalidClient).WithDescription("The specified client and/or credentials are invalid.");
        }

        /// <summary>
        /// Creates an <see cref="IOpenIdError"/> for when processing an <c>OAuth</c> or <c>OpenID Connect</c> message produces an error.
        /// </summary>
        /// <param name="errorDescription">The value for <see cref="IOpenIdError.Description"/>..</param>
        /// <param name="errorCode">The value for <see cref="IOpenIdError.Code"/>. Defaults to <see cref="OpenIdConstants.ErrorCodes.InvalidRequest"/>.</param>
        /// <returns>The newly created <see cref="IOpenIdError"/> instance.</returns>
        public IOpenIdError InvalidRequest(string errorDescription, string errorCode = OpenIdConstants.ErrorCodes.InvalidRequest)
        {
            return factory.Create(errorCode).WithDescription(errorDescription);
        }

        /// <summary>
        /// Creates an <see cref="IOpenIdError"/> for when then provided authorization grant or refresh token is invalid, expired, revoked, does not match the redirection URI used in the authorization request, or was issued to another client.
        /// </summary>
        /// <param name="errorDescription">The value for <see cref="IOpenIdError.Description"/>..</param>
        /// <param name="errorCode">The value for <see cref="IOpenIdError.Code"/>. Defaults to <see cref="OpenIdConstants.ErrorCodes.InvalidGrant"/>.</param>
        /// <returns>The newly created <see cref="IOpenIdError"/> instance.</returns>
        public IOpenIdError InvalidGrant(string errorDescription, string errorCode = OpenIdConstants.ErrorCodes.InvalidGrant)
        {
            return factory.Create(errorCode).WithDescription(errorDescription);
        }

        /// <summary>
        /// Creates an <see cref="IOpenIdError"/> for when then requested scope is invalid, unknown, malformed, or exceeds the scope granted by the resource owner.
        /// </summary>
        /// <returns>The newly created <see cref="IOpenIdError"/> instance.</returns>
        public IOpenIdError InvalidScope()
        {
            return factory.Create(OpenIdConstants.ErrorCodes.InvalidScope).WithDescription("The requested scope is invalid, unknown, malformed, or exceeds the scope granted by the resource owner.");
        }

        /// <summary>
        /// Creates an <see cref="IOpenIdError"/> for when processing the <c>request_uri</c> parameter produces an error.
        /// </summary>
        /// <param name="errorDescription">The value for <see cref="IOpenIdError.Description"/>..</param>
        /// <param name="errorCode">The value for <see cref="IOpenIdError.Code"/>. Defaults to <see cref="OpenIdConstants.ErrorCodes.InvalidRequestUri"/>.</param>
        /// <returns>The newly created <see cref="IOpenIdError"/> instance.</returns>
        public IOpenIdError InvalidRequestUri(string errorDescription, string errorCode = OpenIdConstants.ErrorCodes.InvalidRequestUri)
        {
            return factory.Create(errorCode).WithDescription(errorDescription);
        }

        /// <summary>
        /// Creates an <see cref="IOpenIdError"/> for when a required parameter is missing.
        /// </summary>
        /// <param name="parameterName">The name of the parameter.</param>
        /// <param name="errorCode">The value for <see cref="IOpenIdError.Code"/>. Defaults to <see cref="OpenIdConstants.ErrorCodes.InvalidRequest"/>.</param>
        /// <returns>The newly created <see cref="IOpenIdError"/> instance.</returns>
        public IOpenIdError MissingParameter(string parameterName, string errorCode = OpenIdConstants.ErrorCodes.InvalidRequest)
        {
            return factory.Create(errorCode).WithDescription($"The request is missing the '{parameterName}' parameter.");
        }

        /// <summary>
        /// Creates an <see cref="IOpenIdError"/> for when a parameter contains an invalid value.
        /// </summary>
        /// <param name="parameterName">The name of the parameter.</param>
        /// <param name="errorCode">The value for <see cref="IOpenIdError.Code"/>. Defaults to <see cref="OpenIdConstants.ErrorCodes.InvalidRequest"/>.</param>
        /// <returns>The newly created <see cref="IOpenIdError"/> instance.</returns>
        public IOpenIdError InvalidParameterValue(string parameterName, string errorCode = OpenIdConstants.ErrorCodes.InvalidRequest)
        {
            return factory.Create(errorCode).WithDescription($"The request includes an invalid value for the '{parameterName}' parameter.");
        }

        /// <summary>
        /// Creates an <see cref="IOpenIdError"/> for when a parameter contains more than 1 value.
        /// </summary>
        /// <param name="parameterName">The name of the parameter.</param>
        /// <param name="errorCode">The value for <see cref="IOpenIdError.Code"/>. Defaults to <see cref="OpenIdConstants.ErrorCodes.InvalidRequest"/>.</param>
        /// <returns>The newly created <see cref="IOpenIdError"/> instance.</returns>
        public IOpenIdError TooManyParameterValues(string parameterName, string errorCode = OpenIdConstants.ErrorCodes.InvalidRequest)
        {
            return factory.Create(errorCode).WithDescription($"The request includes the '{parameterName}' parameter more than once.");
        }

        /// <summary>
        /// Creates an <see cref="IOpenIdError"/> for when the <c>request</c> parameter (i.e. request object) is not supported.
        /// </summary>
        /// <param name="errorCode">The value for <see cref="IOpenIdError.Code"/>. Defaults to <see cref="OpenIdConstants.ErrorCodes.RequestNotSupported"/>.</param>
        /// <returns>The newly created <see cref="IOpenIdError"/> instance.</returns>
        public IOpenIdError RequestParameterNotSupported(string errorCode = OpenIdConstants.ErrorCodes.RequestNotSupported)
        {
            return factory.Create(errorCode).WithDescription("The 'request' parameter is not supported.");
        }

        /// <summary>
        /// Creates an <see cref="IOpenIdError"/> for when the <c>request_uri</c> parameter (i.e. request object) is not supported.
        /// </summary>
        /// <param name="errorCode">The value for <see cref="IOpenIdError.Code"/>. Defaults to <see cref="OpenIdConstants.ErrorCodes.RequestUriNotSupported"/>.</param>
        /// <returns>The newly created <see cref="IOpenIdError"/> instance.</returns>
        public IOpenIdError RequestUriNotSupported(string errorCode = OpenIdConstants.ErrorCodes.RequestUriNotSupported)
        {
            return factory.Create(errorCode).WithDescription("The 'request_uri' parameter is not supported.");
        }

        /// <summary>
        /// Creates an <see cref="IOpenIdError"/> for when attempting to decode the request object JWT produces an error.
        /// </summary>
        /// <param name="errorCode">The value for <see cref="IOpenIdError.Code"/>.</param>
        /// <returns>The newly created <see cref="IOpenIdError"/> instance.</returns>
        public IOpenIdError FailedToDecodeJwt(string errorCode)
        {
            return factory.Create(errorCode).WithDescription("An error occurred while attempting to decode the JWT value.");
        }

        /// <summary>
        /// Creates an <see cref="IOpenIdError"/> for when attempting to deserialize the request object JSON produces an error.
        /// </summary>
        /// <param name="errorCode">The value for <see cref="IOpenIdError.Code"/>.</param>
        /// <returns>The newly created <see cref="IOpenIdError"/> instance.</returns>
        public IOpenIdError FailedToDeserializeJson(string errorCode)
        {
            return factory.Create(errorCode).WithDescription("An error occurred while attempting to deserialize the JSON value.");
        }

        /// <summary>
        /// Creates an <see cref="IOpenIdError"/> for when processing an <c>OAuth</c> or <c>OpenID Connect</c>
        /// message produces an <see cref="OpenIdConstants.ErrorCodes.UnsupportedGrantType"/> error.
        /// </summary>
        /// <param name="errorDescription">The value for <see cref="IOpenIdError.Description"/>..</param>
        /// <returns>The newly created <see cref="IOpenIdError"/> instance.</returns>
        public IOpenIdError UnsupportedGrantType(string errorDescription)
        {
            return factory.Create(OpenIdConstants.ErrorCodes.UnsupportedGrantType).WithDescription(errorDescription);
        }

        /// <summary>
        /// Creates an <see cref="IOpenIdError"/> for when processing an <c>OAuth</c> or <c>OpenID Connect</c>
        /// message produces an <see cref="OpenIdConstants.ErrorCodes.UnauthorizedClient"/> error.
        /// </summary>
        /// <param name="errorDescription">The value for <see cref="IOpenIdError.Description"/>..</param>
        /// <returns>The newly created <see cref="IOpenIdError"/> instance.</returns>
        public IOpenIdError UnauthorizedClient(string errorDescription)
        {
            return factory.Create(OpenIdConstants.ErrorCodes.UnauthorizedClient).WithDescription(errorDescription);
        }

        /// <summary>
        /// Creates an <see cref="IOpenIdError"/> for when processing an <c>OAuth</c> or <c>OpenID Connect</c>
        /// message produces an <see cref="OpenIdConstants.ErrorCodes.LoginRequired"/> error.
        /// </summary>
        /// <returns>The newly created <see cref="IOpenIdError"/> instance.</returns>
        public IOpenIdError LoginRequired()
        {
            return factory.Create(OpenIdConstants.ErrorCodes.LoginRequired);
        }

        /// <summary>
        /// Creates an <see cref="IOpenIdError"/> for when processing an <c>OAuth</c> or <c>OpenID Connect</c>
        /// message produces an <see cref="OpenIdConstants.ErrorCodes.AccessDenied"/> error.
        /// </summary>
        /// <param name="errorDescription">The value for <see cref="IOpenIdError.Description"/>..</param>
        /// <returns>The newly created <see cref="IOpenIdError"/> instance.</returns>
        public IOpenIdError AccessDenied(string errorDescription)
        {
            return factory.Create(OpenIdConstants.ErrorCodes.AccessDenied).WithDescription(errorDescription);
        }

        /// <summary>
        /// Creates an <see cref="IOpenIdError"/> for when a parameter value is not supported.
        /// </summary>
        /// <param name="parameterName">The name of the parameter.</param>
        /// <returns>The newly created <see cref="IOpenIdError"/> instance.</returns>
        public IOpenIdError NotSupported(string parameterName)
        {
            return factory.Create(OpenIdConstants.ErrorCodes.InvalidRequest).WithDescription($"The supplied value in the '{parameterName}' parameter is not supported by the authorization server.");
        }
    }
}
