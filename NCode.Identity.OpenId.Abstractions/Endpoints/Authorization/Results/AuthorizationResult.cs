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

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Results;

namespace NCode.Identity.OpenId.Endpoints.Authorization.Results;

/// <summary>
/// An implementation of <see cref="IResult"/> that when executed, issues the response for an
/// <c>OAuth</c> or <c>OpenID Connect</c> authorization operation. This result is only used when the
/// request has been validated, specifically the <c>client_id</c> and <c>redirect_uri</c> parameters
/// from the request have been validated, and it is safe to redirect the user agent back to the client.
/// </summary>
[PublicAPI]
public class AuthorizationResult : IResult, ISupportError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizationResult"/> class.
    /// </summary>
    /// <param name="redirectUri">The <see cref="Uri"/> where the authorization response should be sent to.</param>
    /// <param name="responseMode">The mechanism that should be used for sending the authorization response.</param>
    /// <param name="error">The <see cref="IOpenIdError"/> that contains information about the failure of the operation.</param>
    public AuthorizationResult(Uri redirectUri, string responseMode, IOpenIdError error)
        : this(redirectUri, responseMode, error, null)
    {
        Debug.Assert(error.State is not null);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizationResult"/> class.
    /// </summary>
    /// <param name="redirectUri">The <see cref="Uri"/> where the authorization response should be sent to.</param>
    /// <param name="responseMode">The mechanism that should be used for sending the authorization response.</param>
    /// <param name="ticket">An <see cref="IAuthorizationTicket"/> that contains the parameters for a successful
    /// <c>OAuth</c> or <c>OpenID Connect</c> authorization response.</param>
    public AuthorizationResult(Uri redirectUri, string responseMode, IAuthorizationTicket ticket)
        : this(redirectUri, responseMode, null, ticket)
    {
        Debug.Assert(ticket.State is not null);
    }

    private AuthorizationResult(Uri redirectUri, string responseMode, IOpenIdError? error, IAuthorizationTicket? ticket)
    {
        RedirectUri = redirectUri;
        ResponseMode = responseMode;
        Error = error;
        Ticket = ticket;
    }

    /// <summary>
    /// Gets the <see cref="Uri"/> where the authorization response should be sent to.
    /// </summary>
    public Uri RedirectUri { get; }

    /// <summary>
    /// Gets the mechanism that should be used for sending the authorization response.
    /// </summary>
    public string ResponseMode { get; }

    /// <summary>
    /// Gets a value indicating whether the authorization operation was successful.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Error))]
    [MemberNotNullWhen(true, nameof(Ticket))]
    public bool Succeeded => Ticket != null;

    /// <summary>
    /// Gets the <see cref="IOpenIdError"/> that contains information about the failure of the operation.
    /// </summary>
    public IOpenIdError? Error { get; }

    /// <summary>
    /// Gets the <see cref="IAuthorizationTicket"/> that contains the parameters for a successful
    /// <c>OAuth</c> or <c>OpenID Connect</c> authorization response.
    /// </summary>
    public IAuthorizationTicket? Ticket { get; }

    /// <inheritdoc />
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        var executor = httpContext.RequestServices.GetRequiredService<IResultExecutor<AuthorizationResult>>();
        await executor.ExecuteAsync(httpContext, this, httpContext.RequestAborted);
    }
}
