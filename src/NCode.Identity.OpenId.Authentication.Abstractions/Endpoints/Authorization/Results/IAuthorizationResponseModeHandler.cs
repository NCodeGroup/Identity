// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Results;

/// <summary>
/// Delivers an authorization response to the client in one <c>response_mode</c> (OAuth 2.0 Multiple Response Type
/// Encoding Practices §2.1, OAuth 2.0 Form Post Response Mode, JARM §2.3).
/// </summary>
/// <remarks>
/// Response modes are an open vocabulary: when the server derives the delivery of a response it resolves the handler
/// whose <see cref="ResponseMode"/> serves the request, and the result carries it as
/// <see cref="AuthorizationResponseDelivery.ResponseModeHandler"/>, so a package adds a response mode by registering a
/// handler. The built-in handlers deliver <c>query</c>, <c>fragment</c>, <c>form_post</c>, and their JWT forms
/// <c>query.jwt</c>, <c>fragment.jwt</c>, and <c>form_post.jwt</c>. A handler declares its traits; nothing is inferred
/// from its name.
/// </remarks>
// See ADR-1150 and ADR-1201.
[PublicAPI]
public interface IAuthorizationResponseModeHandler
{
    /// <summary>
    /// Gets the <c>response_mode</c> value this handler delivers.
    /// </summary>
    string ResponseMode { get; }

    /// <summary>
    /// Gets the response mode this handler delivers in JWT-secured form (JARM §2.3), such as <c>query</c> for
    /// <c>query.jwt</c>; <see langword="null"/> when it delivers the response parameters as they are. The JARM
    /// <c>jwt</c> shorthand resolves to the handler that secures the response type's default response mode.
    /// </summary>
    string? SecuredResponseMode { get; }

    /// <summary>
    /// Gets a value indicating whether the response travels in the redirect URI's query string, where no token may
    /// appear (OAuth 2.0 Multiple Response Type Encoding Practices §2.1, JARM §2.3.1).
    /// </summary>
    bool DeliversInQuery { get; }

    /// <summary>
    /// Writes the authorization response to the HTTP response.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> of the authorization request.</param>
    /// <param name="result">The <see cref="AuthorizationResult"/> to deliver.</param>
    /// <param name="message">The response message: the result's ticket, or its error.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask ExecuteAsync(
        HttpContext httpContext,
        AuthorizationResult result,
        IOpenIdMessageResponse message,
        CancellationToken cancellationToken
    );
}
