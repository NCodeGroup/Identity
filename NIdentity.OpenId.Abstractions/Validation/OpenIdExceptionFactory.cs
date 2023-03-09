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

using Microsoft.AspNetCore.Http;

namespace NIdentity.OpenId.Validation;

/// <summary>
/// Provides the ability to create an <see cref="OpenIdException"/> instance from an <c>OAuth</c> or <c>OpenID Connect</c> error.
/// </summary>
public interface IOpenIdExceptionFactory
{
    /// <summary>
    /// Creates an <see cref="OpenIdException"/> instance from an <c>OAuth</c> or <c>OpenID Connect</c> error.
    /// </summary>
    /// <param name="errorCode">The error code value.</param>
    /// <param name="innerException">The optional inner <see cref="Exception"/> if any.</param>
    /// <returns>The newly created <see cref="OpenIdException"/> instance.</returns>
    OpenIdException Create(string errorCode, Exception? innerException = null);
}

internal class OpenIdExceptionFactory : IOpenIdExceptionFactory
{
    public static IOpenIdExceptionFactory Instance { get; } = new OpenIdExceptionFactory();

    /// <inheritdoc />
    public OpenIdException Create(string errorCode, Exception? innerException = null)
    {
        var message = GetMessageFromErrorCode(errorCode);
        var statusCode = GetStatusCodeFromErrorCode(errorCode);
        return new OpenIdException(message, errorCode, statusCode, innerException);
    }

    private static int GetStatusCodeFromErrorCode(string errorCode) => errorCode switch
    {
        // TODO: translate other error codes
        OpenIdConstants.ErrorCodes.ServerError => StatusCodes.Status500InternalServerError,
        _ => StatusCodes.Status400BadRequest
    };

    private static string GetMessageFromErrorCode(string errorCode) => errorCode switch
    {
        OpenIdConstants.ErrorCodes.AccessDenied => "The resource owner or authorization server denied the request.",
        OpenIdConstants.ErrorCodes.AccountSelectionRequired => "The End-User is REQUIRED to select a session at the Authorization Server.",
        OpenIdConstants.ErrorCodes.ConsentRequired => "The Authorization Server requires End-User consent.",
        OpenIdConstants.ErrorCodes.InteractionRequired => "The Authorization Server requires End-User interaction of some form to proceed.",
        OpenIdConstants.ErrorCodes.InvalidRequest => "The request is missing a required parameter, includes an invalid parameter value, includes a parameter more than once, or is otherwise malformed.",
        OpenIdConstants.ErrorCodes.InvalidRequestJwt => "The request parameter contains an invalid Request Object.",
        OpenIdConstants.ErrorCodes.InvalidRequestUri => "The request_uri in the Authorization Request contains an error or invalid data.",
        OpenIdConstants.ErrorCodes.InvalidScope => "The requested scope is invalid, unknown, or malformed.",
        OpenIdConstants.ErrorCodes.LoginRequired => "The Authorization Server requires End-User authentication.",
        OpenIdConstants.ErrorCodes.RegistrationNotSupported => "The OP does not support use of the registration parameter.",
        OpenIdConstants.ErrorCodes.RequestNotSupported => "The OP does not support use of the request parameter.",
        OpenIdConstants.ErrorCodes.RequestUriNotSupported => "The OP does not support use of the request_uri parameter.",
        OpenIdConstants.ErrorCodes.TemporarilyUnavailable => "The authorization server is currently unable to handle the request due to a temporary overloading or maintenance of the server.",
        OpenIdConstants.ErrorCodes.UnauthorizedClient => "The client is not authorized to request an authorization code using this method.",
        OpenIdConstants.ErrorCodes.UnsupportedResponseType => "The authorization server does not support obtaining an authorization code using this method.",
        _ => "The authorization server encountered an unexpected condition that prevented it from fulfilling the request.",
    };
}
