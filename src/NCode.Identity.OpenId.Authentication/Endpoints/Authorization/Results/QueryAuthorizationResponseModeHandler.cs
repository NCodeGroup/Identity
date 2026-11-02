// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Results;

/// <summary>
/// Delivers the <c>query</c> response mode: a redirect with the response parameters in the query string.
/// </summary>
internal sealed class QueryAuthorizationResponseModeHandler : IAuthorizationResponseModeHandler
{
    public string ResponseMode => OpenIdProtocol.ResponseModes.Query;

    public string? SecuredResponseMode => null;

    public bool DeliversInQuery => true;

    public ValueTask ExecuteAsync(
        HttpContext httpContext,
        AuthorizationResult result,
        IOpenIdMessageResponse message,
        CancellationToken cancellationToken
    )
    {
        AuthorizationResponseWriter.WriteRedirect(
            httpContext.Response,
            result.Delivery.RedirectUri,
            useQuery: true,
            AuthorizationResponseWriter.GetParameters(message)
        );
        return ValueTask.CompletedTask;
    }
}
