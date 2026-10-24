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

using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token;

/// <summary>
/// Validates the subject a token grant issues tokens for, and throws the refusal as an error response.
/// </summary>
internal static class TokenGrantSubjectValidation
{
    /// <summary>
    /// Validates the subject a token grant issues tokens for, and throws the refusal as an error response with the
    /// HTTP status code 400.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
    /// <param name="openIdClient">The <see cref="OpenIdClient"/> the token request is for.</param>
    /// <param name="openIdRequest">The <see cref="IOpenIdRequest"/> being processed.</param>
    /// <param name="subjectAuthentication">The <see cref="SubjectAuthentication"/> identifying the end-user to validate.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe.</param>
    public static async ValueTask ValidateAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        IOpenIdRequest openIdRequest,
        SubjectAuthentication subjectAuthentication,
        CancellationToken cancellationToken
    )
    {
        var mediator = openIdContext.Mediator;
        var operationDisposition = new OperationDisposition<IOpenIdError>();

        await mediator.SendAsync(
            new ValidateSubjectAuthenticationCommand(
                openIdContext,
                openIdClient,
                openIdRequest,
                subjectAuthentication,
                operationDisposition,
                // A token grant validates the subject against a later clock than its authentication, so the
                // authentication age, which the authorization request enforced, is not checked again.
                EnforceAuthenticationAge: false
            ),
            cancellationToken
        );

        if (operationDisposition.HasError)
        {
            throw operationDisposition
                .Error.WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();
        }
    }
}
