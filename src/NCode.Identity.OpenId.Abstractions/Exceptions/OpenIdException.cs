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

namespace NCode.Identity.OpenId.Exceptions;

// ReSharper disable IntroduceOptionalParameters.Global
// Justification: optional parameters with default values is not backwards compatible and causes breaking changes

/// <summary>
/// Represents an error that occurred while executing an <c>OAuth</c> or <c>OpenID Connect</c> handler.
/// </summary>
[PublicAPI]
public class OpenIdException : Exception
{
    /// <summary>
    /// Gets the <see cref="IOpenIdError"/> that contains detailed error information.
    /// </summary>
    public IOpenIdError Error { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenIdException"/> class with the specified error information.
    /// </summary>
    /// <param name="error">An <see cref="IOpenIdError"/> that contains detailed error information.</param>
    public OpenIdException(IOpenIdError error)
        : this(error, null, null)
    {
        // nothing
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenIdException"/> class with the specified error information
    /// and error message.
    /// </summary>
    /// <param name="error">An <see cref="IOpenIdError"/> that contains detailed error information.</param>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public OpenIdException(IOpenIdError error, string? message)
        : this(error, message, null)
    {
        // nothing
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenIdException"/> class with the specified error information
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="error">An <see cref="IOpenIdError"/> that contains detailed error information.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a <c>null</c>
    /// reference if no inner exception is specified.</param>
    public OpenIdException(IOpenIdError error, Exception? innerException)
        : this(error, null, innerException)
    {
        // nothing
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenIdException"/> class with the specified error information,
    /// error message, a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="error">An <see cref="IOpenIdError"/> that contains detailed error information.</param>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a <c>null</c>
    /// reference if no inner exception is specified.</param>
    public OpenIdException(IOpenIdError error, string? message, Exception? innerException)
        : base(message ?? GetDefaultMessageFromErrorCode(error.Code), innerException)
    {
        Error = error;
    }

    private static string GetDefaultMessageFromErrorCode(string errorCode) =>
        errorCode switch
        {
            OpenIdProtocol.ErrorCodes.AccessDenied =>
                "The resource owner or authorization server denied the request.",
            OpenIdProtocol.ErrorCodes.AccountSelectionRequired =>
                "The End-User is REQUIRED to select a session at the Authorization Server.",
            OpenIdProtocol.ErrorCodes.ConsentRequired =>
                "The Authorization Server requires End-User consent.",
            OpenIdProtocol.ErrorCodes.InteractionRequired =>
                "The Authorization Server requires End-User interaction of some form to proceed.",
            OpenIdProtocol.ErrorCodes.InvalidRequest =>
                "The request is missing a required parameter, includes an invalid parameter value, includes a parameter more than once, or is otherwise malformed.",
            OpenIdProtocol.ErrorCodes.InvalidRequestJwt =>
                "The request parameter contains an invalid Request Object.",
            OpenIdProtocol.ErrorCodes.InvalidRequestUri =>
                "The request_uri in the Authorization Request contains an error or invalid data.",
            OpenIdProtocol.ErrorCodes.InvalidScope =>
                "The requested scope is invalid, unknown, or malformed.",
            OpenIdProtocol.ErrorCodes.LoginRequired =>
                "The Authorization Server requires End-User authentication.",
            OpenIdProtocol.ErrorCodes.RegistrationNotSupported =>
                "The OP does not support use of the registration parameter.",
            OpenIdProtocol.ErrorCodes.RequestNotSupported =>
                "The OP does not support use of the request parameter.",
            OpenIdProtocol.ErrorCodes.RequestUriNotSupported =>
                "The OP does not support use of the request_uri parameter.",
            OpenIdProtocol.ErrorCodes.TemporarilyUnavailable =>
                "The authorization server is currently unable to handle the request due to a temporary overloading or maintenance of the server.",
            OpenIdProtocol.ErrorCodes.UnauthorizedClient =>
                "The client is not authorized to request an authorization code using this method.",
            OpenIdProtocol.ErrorCodes.UnsupportedResponseType =>
                "The authorization server does not support obtaining an authorization code using this method.",
            _ =>
                "The authorization server encountered an unexpected condition that prevented it from fulfilling the request.",
        };
}
