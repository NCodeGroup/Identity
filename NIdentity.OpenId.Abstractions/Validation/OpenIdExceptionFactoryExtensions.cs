#region Copyright Preamble

//
//    Copyright @ 2021 NCode Group
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

using System;
using Microsoft.AspNetCore.Http;

namespace NIdentity.OpenId.Validation
{
    /// <summary>
    /// Provides extension methods for <see cref="IOpenIdExceptionFactory"/> that can be used to create specific
    /// <c>OAuth</c> or <c>OpenID Connect</c> errors.
    /// </summary>
    public static class OpenIdExceptionFactoryExtensions
    {
        private static OpenIdException Create(IOpenIdExceptionFactory factory, string errorCode)
        {
            return factory.Create(errorCode).WithStatusCode(StatusCodes.Status400BadRequest);
        }

        private static OpenIdException Create(IOpenIdExceptionFactory factory, string errorCode, Exception? innerException)
        {
            return factory.Create(errorCode, innerException).WithStatusCode(StatusCodes.Status400BadRequest);
        }

        /// <summary>
        /// Creates an <see cref="OpenIdException"/> for when processing an <c>OAuth</c> or <c>OpenID Connect</c> message produces an error.
        /// </summary>
        /// <param name="factory">The <see cref="IOpenIdExceptionFactory"/> instance.</param>
        /// <param name="errorDescription">The value for <see cref="IErrorDetails.Description"/>..</param>
        /// <param name="errorCode">The value for <see cref="IErrorDetails.Code"/>. Defaults to <see cref="OpenIdConstants.ErrorCodes.InvalidRequest"/>.</param>
        /// <returns>The newly created <see cref="OpenIdException"/> instance.</returns>
        public static OpenIdException InvalidRequest(this IOpenIdExceptionFactory factory, string errorDescription, string errorCode = OpenIdConstants.ErrorCodes.InvalidRequest)
        {
            return Create(factory, errorCode).WithErrorDescription(errorDescription);
        }

        /// <summary>
        /// Creates an <see cref="OpenIdException"/> for when processing the <c>request_uri</c> parameter produces an error.
        /// </summary>
        /// <param name="factory">The <see cref="IOpenIdExceptionFactory"/> instance.</param>
        /// <param name="errorDescription">The value for <see cref="IErrorDetails.Description"/>..</param>
        /// <param name="errorCode">The value for <see cref="IErrorDetails.Code"/>. Defaults to <see cref="OpenIdConstants.ErrorCodes.InvalidRequestUri"/>.</param>
        /// <returns>The newly created <see cref="OpenIdException"/> instance.</returns>
        public static OpenIdException InvalidRequestUri(this IOpenIdExceptionFactory factory, string errorDescription, string errorCode = OpenIdConstants.ErrorCodes.InvalidRequestUri)
        {
            return Create(factory, errorCode).WithErrorDescription(errorDescription);
        }

        /// <summary>
        /// Creates an <see cref="OpenIdException"/> for when attempting to fetch <c>request_uri</c> produces an error.
        /// </summary>
        /// <param name="factory">The <see cref="IOpenIdExceptionFactory"/> instance.</param>
        /// <param name="innerException">The inner <see cref="Exception"/>.</param>
        /// <param name="errorCode">The value for <see cref="IErrorDetails.Code"/>. Defaults to <see cref="OpenIdConstants.ErrorCodes.InvalidRequestUri"/>.</param>
        /// <returns>The newly created <see cref="OpenIdException"/> instance.</returns>
        public static OpenIdException InvalidRequestUri(this IOpenIdExceptionFactory factory, Exception innerException, string errorCode = OpenIdConstants.ErrorCodes.InvalidRequestUri)
        {
            return Create(factory, errorCode, innerException).WithErrorDescription("An error occurred while attempting to fetch request_uri.");
        }

        /// <summary>
        /// Creates an <see cref="OpenIdException"/> for when a required parameter is missing.
        /// </summary>
        /// <param name="factory">The <see cref="IOpenIdExceptionFactory"/> instance.</param>
        /// <param name="parameterName">The name of the parameter.</param>
        /// <param name="errorCode">The value for <see cref="IErrorDetails.Code"/>. Defaults to <see cref="OpenIdConstants.ErrorCodes.InvalidRequest"/>.</param>
        /// <returns>The newly created <see cref="OpenIdException"/> instance.</returns>
        public static OpenIdException MissingParameter(this IOpenIdExceptionFactory factory, string parameterName, string errorCode = OpenIdConstants.ErrorCodes.InvalidRequest)
        {
            return Create(factory, errorCode).WithErrorDescription($"The request is missing the {parameterName} parameter.");
        }

        /// <summary>
        /// Creates an <see cref="OpenIdException"/> for when a parameter contains an invalid value.
        /// </summary>
        /// <param name="factory">The <see cref="IOpenIdExceptionFactory"/> instance.</param>
        /// <param name="parameterName">The name of the parameter.</param>
        /// <param name="errorCode">The value for <see cref="IErrorDetails.Code"/>. Defaults to <see cref="OpenIdConstants.ErrorCodes.InvalidRequest"/>.</param>
        /// <returns>The newly created <see cref="OpenIdException"/> instance.</returns>
        public static OpenIdException InvalidParameterValue(this IOpenIdExceptionFactory factory, string parameterName, string errorCode = OpenIdConstants.ErrorCodes.InvalidRequest)
        {
            return Create(factory, errorCode).WithErrorDescription($"The request includes an invalid value for the {parameterName} parameter.");
        }

        /// <summary>
        /// Creates an <see cref="OpenIdException"/> for when a parameter contains more than 1 value.
        /// </summary>
        /// <param name="factory">The <see cref="IOpenIdExceptionFactory"/> instance.</param>
        /// <param name="parameterName">The name of the parameter.</param>
        /// <param name="errorCode">The value for <see cref="IErrorDetails.Code"/>. Defaults to <see cref="OpenIdConstants.ErrorCodes.InvalidRequest"/>.</param>
        /// <returns>The newly created <see cref="OpenIdException"/> instance.</returns>
        public static OpenIdException TooManyParameterValues(this IOpenIdExceptionFactory factory, string parameterName, string errorCode = OpenIdConstants.ErrorCodes.InvalidRequest)
        {
            return Create(factory, errorCode).WithErrorDescription($"The request includes the {parameterName} parameter more than once.");
        }

        /// <summary>
        /// Creates an <see cref="OpenIdException"/> for when the <c>request</c> parameter (i.e. request object) is not supported.
        /// </summary>
        /// <param name="factory">The <see cref="IOpenIdExceptionFactory"/> instance.</param>
        /// <param name="errorCode">The value for <see cref="IErrorDetails.Code"/>. Defaults to <see cref="OpenIdConstants.ErrorCodes.RequestNotSupported"/>.</param>
        /// <returns>The newly created <see cref="OpenIdException"/> instance.</returns>
        public static OpenIdException RequestJwtNotSupported(this IOpenIdExceptionFactory factory, string errorCode = OpenIdConstants.ErrorCodes.RequestNotSupported)
        {
            return Create(factory, errorCode).WithErrorDescription("The 'request' parameter is not supported.");
        }

        /// <summary>
        /// Creates an <see cref="OpenIdException"/> for when the <c>request_uri</c> parameter (i.e. request object) is not supported.
        /// </summary>
        /// <param name="factory">The <see cref="IOpenIdExceptionFactory"/> instance.</param>
        /// <param name="errorCode">The value for <see cref="IErrorDetails.Code"/>. Defaults to <see cref="OpenIdConstants.ErrorCodes.RequestUriNotSupported"/>.</param>
        /// <returns>The newly created <see cref="OpenIdException"/> instance.</returns>
        public static OpenIdException RequestUriNotSupported(this IOpenIdExceptionFactory factory, string errorCode = OpenIdConstants.ErrorCodes.RequestUriNotSupported)
        {
            return Create(factory, errorCode).WithErrorDescription("The request_uri parameter is not supported.");
        }

        /// <summary>
        /// Creates an <see cref="OpenIdException"/> for when attempting to decode the request object JWT produces an error.
        /// </summary>
        /// <param name="factory">The <see cref="IOpenIdExceptionFactory"/> instance.</param>
        /// <param name="errorCode">The value for <see cref="IErrorDetails.Code"/>.</param>
        /// <param name="innerException">The inner <see cref="Exception"/>.</param>
        /// <returns>The newly created <see cref="OpenIdException"/> instance.</returns>
        public static OpenIdException FailedToDecodeJwt(this IOpenIdExceptionFactory factory, string errorCode, Exception? innerException = null)
        {
            return Create(factory, errorCode, innerException).WithErrorDescription("An error occurred while attempting to decode the JWT value.");
        }

        /// <summary>
        /// Creates an <see cref="OpenIdException"/> for when attempting to deserialize the request object JSON produces an error.
        /// </summary>
        /// <param name="factory">The <see cref="IOpenIdExceptionFactory"/> instance.</param>
        /// <param name="errorCode">The value for <see cref="IErrorDetails.Code"/>.</param>
        /// <param name="innerException">The inner <see cref="Exception"/>.</param>
        /// <returns>The newly created <see cref="OpenIdException"/> instance.</returns>
        public static OpenIdException FailedToDeserializeJson(this IOpenIdExceptionFactory factory, string errorCode, Exception? innerException = null)
        {
            return Create(factory, errorCode, innerException).WithErrorDescription("An error occurred while attempting to deserialize the JSON value.");
        }

        /// <summary>
        /// Creates an <see cref="OpenIdException"/> for when processing an <c>OAuth</c> or <c>OpenID Connect</c>
        /// message produces an <see cref="OpenIdConstants.ErrorCodes.UnauthorizedClient"/> error.
        /// </summary>
        /// <param name="factory">The <see cref="IOpenIdExceptionFactory"/> instance.</param>
        /// <param name="errorDescription">The value for <see cref="IErrorDetails.Description"/>..</param>
        /// <returns>The newly created <see cref="OpenIdException"/> instance.</returns>
        public static OpenIdException UnauthorizedClient(this IOpenIdExceptionFactory factory, string errorDescription)
        {
            return Create(factory, OpenIdConstants.ErrorCodes.UnauthorizedClient).WithErrorDescription(errorDescription);
        }
    }
}
