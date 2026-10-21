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

using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Messages;
using NCode.Identity.OpenId.Contexts;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Endpoints.EndSession;

/// <summary>
/// Provides a default implementation of the required services and handlers used by the <c>OpenID Connect</c>
/// RP-Initiated Logout <c>end_session_endpoint</c>
/// (<see href="https://openid.net/specs/openid-connect-rpinitiated-1_0.html">RP-Initiated Logout 1.0</see>). The
/// endpoint is a thin HTTP shell that dispatches the <see cref="EndSessionCommand"/> and either redirects to the
/// validated <c>post_logout_redirect_uri</c> or renders a logged-out confirmation.
/// </summary>
internal class DefaultEndSessionEndpointProvider : IEndpointProvider
{
    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapMethods(
                OpenIdConstants.EndpointPaths.EndSession,
                [HttpMethods.Get, HttpMethods.Post],
                HandleRouteAsync
            )
            .WithName(OpenIdConstants.EndpointNames.EndSession)
            .WithTags(OpenIdConstants.EndpointTags.OpenId)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status302Found)
            .DisableAntiforgery()
            .WithOpenIdDiscoverable()
            .WithOpenIdFrontChannel();

    /// <summary>
    /// End session endpoint (OpenID Connect RP-Initiated Logout).
    /// </summary>
    /// <remarks>
    /// Logs the end-user out of the OpenID Provider: it clears the OP login, revokes the session and the subject's
    /// grants, notifies participating relying parties via back-channel logout, and redirects to the validated
    /// <c>post_logout_redirect_uri</c> when one is supplied.
    /// </remarks>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The end-user has been logged out (no valid post-logout redirect was supplied).</response>
    /// <response code="302">The end-user has been logged out and is redirected to the validated post-logout URI.</response>
    internal virtual async ValueTask<IResult> HandleRouteAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var openIdContext = httpContext.GetOpenIdContext();
        var mediator = openIdContext.Mediator;

        var request = await mediator.SendAsync<LoadEndSessionRequestCommand, IEndSessionRequest>(
            new LoadEndSessionRequestCommand(openIdContext),
            cancellationToken
        );

        var disposition = await mediator.SendAsync<EndSessionCommand, EndSessionDisposition>(
            new EndSessionCommand(openIdContext, request),
            cancellationToken
        );

        // When front-channel logout URIs are present, the OP must render a page that loads each one in a hidden iframe
        // so the participating clients clear their own sessions before the user agent leaves the OP.
        if (disposition.FrontChannelLogoutUris.Count > 0)
        {
            var html = BuildFrontChannelLogoutHtml(
                disposition.FrontChannelLogoutUris,
                disposition.RedirectUri
            );
            return TypedResults.Content(html, "text/html; charset=utf-8");
        }

        if (!string.IsNullOrEmpty(disposition.RedirectUri))
        {
            return TypedResults.Redirect(disposition.RedirectUri);
        }

        return TypedResults.Content("You have been logged out.", "text/plain");
    }

    // Builds the Front-Channel Logout 1.0 page: a hidden iframe per participating client, then a scripted redirect to
    // the validated post-logout URI (or a plain confirmation when none was supplied). All URIs are registered client
    // values but are HTML-encoded defensively before they are embedded in the markup.
    private static string BuildFrontChannelLogoutHtml(
        IReadOnlyList<string> frontChannelLogoutUris,
        string? redirectUri
    )
    {
        var encoder = HtmlEncoder.Default;
        var builder = new StringBuilder();
        builder.Append(
            "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>Logout</title></head><body>"
        );

        foreach (var uri in frontChannelLogoutUris)
        {
            builder
                .Append("<iframe style=\"display:none\" src=\"")
                .Append(encoder.Encode(uri))
                .Append("\"></iframe>");
        }

        if (!string.IsNullOrEmpty(redirectUri))
        {
            // Redirect only after the iframes have had a chance to load; the target is read from a DOM attribute so
            // the validated URI is never interpolated into an inline script literal.
            builder
                .Append("<a id=\"pl\" data-url=\"")
                .Append(encoder.Encode(redirectUri))
                .Append("\" style=\"display:none\"></a>")
                .Append(
                    "<script>window.addEventListener('load',function(){setTimeout(function(){"
                        + "window.location.href=document.getElementById('pl').getAttribute('data-url');},2000);});</script>"
                );
        }
        else
        {
            builder.Append("<p>You have been logged out.</p>");
        }

        builder.Append("</body></html>");
        return builder.ToString();
    }
}
