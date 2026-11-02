// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Results;

/// <summary>
/// Delivers the <c>fragment</c> response mode: a redirect with the response parameters in the fragment.
/// </summary>
internal sealed class FragmentAuthorizationResponseModeHandler : IAuthorizationResponseModeHandler
{
    public string ResponseMode => OpenIdProtocol.ResponseModes.Fragment;

    public string? SecuredResponseMode => null;

    public bool DeliversInQuery => false;

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
            useQuery: false,
            AuthorizationResponseWriter.GetParameters(message)
        );
        return ValueTask.CompletedTask;
    }
}
