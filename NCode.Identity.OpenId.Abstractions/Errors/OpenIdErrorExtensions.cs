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
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Errors;

/// <summary>
/// Provides extension methods for the <see cref="IOpenIdError"/> abstraction.
/// </summary>
[PublicAPI]
public static class OpenIdErrorExtensions
{
    /// <param name="error">The <see cref="IOpenIdError"/> to wrap.</param>
    extension(IOpenIdError error)
    {
        /// <summary>
        /// Wraps the <see cref="IOpenIdError"/> in an <see cref="OpenIdException"/>.
        /// </summary>
        /// <returns>The <see cref="OpenIdException"/> instance.</returns>
        public OpenIdException AsException()
        {
            return new OpenIdException(error, error.Exception);
        }

        /// <summary>
        /// Wraps the <see cref="IOpenIdError"/> in an <see cref="OpenIdException"/>.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <returns>The <see cref="OpenIdException"/> instance.</returns>
        public OpenIdException AsException(string? message)
        {
            return new OpenIdException(error, message, error.Exception);
        }

        /// <summary>
        /// Sets the HTTP status code to be used when returning a response.
        /// </summary>
        /// <param name="statusCode">The value to set.</param>
        /// <returns>The <see cref="IOpenIdError"/> instance.</returns>
        public IOpenIdError WithStatusCode(int? statusCode)
        {
            error.StatusCode = statusCode;
            return error;
        }

        /// <summary>
        /// Sets the <see cref="Exception"/> that triggered the <c>OAuth</c> or <c>OpenID Connect</c> error.
        /// </summary>
        /// <param name="exception">The value to set.</param>
        /// <returns>The <see cref="IOpenIdError"/> instance.</returns>
        public IOpenIdError WithException(Exception? exception)
        {
            error.Exception = exception;
            return error;
        }

        /// <summary>
        /// Sets the <c>error_description</c> parameter.
        /// </summary>
        /// <param name="description">The value to set.</param>
        /// <returns>The <see cref="IOpenIdError"/> instance.</returns>
        public IOpenIdError WithDescription(string? description)
        {
            error.Description = description;
            return error;
        }

        /// <summary>
        /// Sets the <c>error_uri</c> parameter.
        /// </summary>
        /// <param name="uri">The value to set.</param>
        /// <returns>The <see cref="IOpenIdError"/> instance.</returns>
        public IOpenIdError WithUri(Uri? uri)
        {
            error.Uri = uri;
            return error;
        }

        /// <summary>
        /// Sets the <c>state</c> parameter.
        /// </summary>
        /// <param name="state">The value to set.</param>
        /// <returns>The <see cref="IOpenIdError"/> instance.</returns>
        public IOpenIdError WithState(string? state)
        {
            error.State = state;
            return error;
        }

        /// <summary>
        /// Sets the <c>error</c> parameter.
        /// </summary>
        /// <param name="code">The value to set.</param>
        /// <returns>The <see cref="IOpenIdError"/> instance.</returns>
        public IOpenIdError WithCode(string code)
        {
            error.Code = code;
            return error;
        }
    }
}
