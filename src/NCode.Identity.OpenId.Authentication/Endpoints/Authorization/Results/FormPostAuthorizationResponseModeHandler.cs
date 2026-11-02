// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Results;

/// <summary>
/// Delivers the <c>form_post</c> response mode: an auto-submitting form that posts the response parameters.
/// </summary>
internal sealed class FormPostAuthorizationResponseModeHandler : IAuthorizationResponseModeHandler
{
    public string ResponseMode => OpenIdProtocol.ResponseModes.FormPost;

    public string? SecuredResponseMode => null;

    public bool DeliversInQuery => false;

    public ValueTask ExecuteAsync(
        HttpContext httpContext,
        AuthorizationResult result,
        IOpenIdMessageResponse message,
        CancellationToken cancellationToken
    ) =>
        AuthorizationResponseWriter.WriteFormPostAsync(
            httpContext.Response,
            result.Delivery.RedirectUri,
            AuthorizationResponseWriter.GetParameters(message),
            cancellationToken
        );
}
