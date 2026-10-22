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

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.Results;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Results;

/// <summary>
/// An implementation of <see cref="IResult"/> that when executed, issues the response for an
/// <c>OAuth</c> or <c>OpenID Connect</c> authorization operation. This result is only used when the
/// request has been validated, specifically the <c>client_id</c> and <c>redirect_uri</c> parameters
/// from the request have been validated, and it is safe to redirect the user agent back to the client.
/// The result always carries the request's <see cref="Contexts.OpenIdContext"/>, the client the response is
/// addressed to, and the <see cref="AuthorizationResponseDelivery"/> that says where and how the response is sent.
/// </summary>
[PublicAPI]
public sealed class AuthorizationResult : IResult, ISupportOpenIdError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizationResult"/> class for a failed operation.
    /// </summary>
    /// <param name="openIdContext">The <see cref="Contexts.OpenIdContext"/> of the request.</param>
    /// <param name="openIdClient">The client the response is addressed to.</param>
    /// <param name="delivery">The <see cref="AuthorizationResponseDelivery"/> that describes how the response is
    /// delivered.</param>
    /// <param name="error">The <see cref="IOpenIdError"/> that contains information about the failure of the operation.</param>
    public AuthorizationResult(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        AuthorizationResponseDelivery delivery,
        IOpenIdError error
    )
        : this(openIdContext, openIdClient, delivery, error, null)
    {
        // 'state' is echoed back only when the client sent it (OAuth 2.0 §4.1.2.1); a null state is valid and omitted.
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizationResult"/> class for a successful operation.
    /// </summary>
    /// <param name="openIdContext">The <see cref="Contexts.OpenIdContext"/> of the request.</param>
    /// <param name="openIdClient">The client the response is addressed to.</param>
    /// <param name="delivery">The <see cref="AuthorizationResponseDelivery"/> that describes how the response is
    /// delivered.</param>
    /// <param name="ticket">An <see cref="IAuthorizationTicket"/> that contains the parameters for a successful
    /// <c>OAuth</c> or <c>OpenID Connect</c> authorization response.</param>
    public AuthorizationResult(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        AuthorizationResponseDelivery delivery,
        IAuthorizationTicket ticket
    )
        : this(openIdContext, openIdClient, delivery, null, ticket)
    {
        // 'state' is echoed back only when the client sent it (OAuth 2.0 §4.1.2.1); a null state is valid and omitted.
    }

    private AuthorizationResult(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        AuthorizationResponseDelivery delivery,
        IOpenIdError? error,
        IAuthorizationTicket? ticket
    )
    {
        Delivery = delivery;
        OpenIdContext = openIdContext;
        OpenIdClient = openIdClient;
        Error = error;
        Ticket = ticket;
    }

    /// <summary>
    /// Gets the <see cref="AuthorizationResponseDelivery"/> that says where and how the response is delivered.
    /// </summary>
    public AuthorizationResponseDelivery Delivery { get; }

    /// <summary>
    /// Gets the request's <see cref="Contexts.OpenIdContext"/>.
    /// </summary>
    public OpenIdContext OpenIdContext { get; }

    /// <summary>
    /// Gets the client the response is addressed to.
    /// </summary>
    public OpenIdClient OpenIdClient { get; }

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
        var executor = httpContext.RequestServices.GetRequiredService<
            IResultExecutor<AuthorizationResult>
        >();
        await executor.ExecuteAsync(httpContext, this, httpContext.RequestAborted);
    }
}
