// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NCode.AspNetCore;
using NCode.Buffers;
using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Endpoints.CheckSession;

/// <summary>
/// Serves the OpenID Connect Session Management 1.0 <c>check_session_iframe</c>: an OP-hosted HTML document whose script
/// recomputes the <c>session_state</c> from the OP browser-state cookie and replies <c>changed</c>/<c>unchanged</c> to
/// the relying party's <c>postMessage</c> poll. The endpoint is only advertised in discovery when the
/// <c>session_management_supported</c> setting is enabled (see
/// <see cref="Discovery.Handlers.DefaultDiscoverSessionManagementHandler"/>).
/// </summary>
internal class DefaultCheckSessionEndpointProvider : IEndpointProvider
{
    // The salt travels inside the session_state the RP sends, so the script digests exactly the same UTF-8 string the
    // server did (client_id, the message origin, the browser state, and the salt), then Base64Url-encodes (unpadded).
    private const string Script =
        "(function(){"
        + "var cookieName='"
        + DefaultSessionStateService.BrowserStateCookieName
        + "';"
        + "function getBrowserState(){var m=document.cookie.match(new RegExp('(?:^|; )'+cookieName+'=([^;]*)'));return m?decodeURIComponent(m[1]):'';}"
        + "function b64url(buf){var b=new Uint8Array(buf),s='';for(var i=0;i<b.length;i++){s+=String.fromCharCode(b[i]);}return btoa(s).replace(/\\+/g,'-').replace(/\\//g,'_').replace(/=+$/,'');}"
        + "function compute(clientId,origin,opbs,salt){return crypto.subtle.digest('SHA-256',new TextEncoder().encode(clientId+' '+origin+' '+opbs+' '+salt)).then(function(d){return b64url(d)+'.'+salt;});}"
        + "window.addEventListener('message',function(e){try{"
        + "var parts=String(e.data).split(' '),clientId=parts[0],sessionState=parts[1];"
        + "if(!clientId||!sessionState){e.source.postMessage('error',e.origin);return;}"
        + "var salt=sessionState.split('.')[1];"
        + "compute(clientId,e.origin,getBrowserState(),salt).then(function(expected){"
        + "e.source.postMessage(expected===sessionState?'unchanged':'changed',e.origin);"
        + "}).catch(function(){e.source.postMessage('error',e.origin);});"
        + "}catch(ex){e.source.postMessage('error',e.origin);}},false);"
        + "})();";

    private const string Html =
        "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>Check Session</title></head>"
        + "<body><script>"
        + Script
        + "</script></body></html>";

    // Relying parties frame this document cross-site by design, so it is the one page that may be framed. See ADR-1256.
    private static HtmlPageSecurityPolicy Policy { get; } =
        new()
        {
            Directives =
            [
                new(
                    ContentSecurityPolicyDirectives.ScriptSrc,
                    ContentSecurityPolicySources.Sha256(Script)
                ),
            ],
            AllowsFraming = true,
        };

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(OpenIdConstants.EndpointPaths.CheckSession, HandleRouteAsync)
            .WithName(OpenIdConstants.EndpointNames.CheckSessionIframe)
            .WithTags(OpenIdConstants.EndpointTags.OpenId)
            .Produces(StatusCodes.Status200OK)
            .DisableAntiforgery();

    /// <summary>
    /// Check session iframe endpoint (OpenID Connect Session Management).
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <returns>The static <c>check_session_iframe</c> HTML document.</returns>
    /// <response code="200">The check session iframe document.</response>
    internal virtual IResult HandleRouteAsync(HttpContext httpContext)
    {
        httpContext.Response.SetHtmlPageSecurity(Policy);
        return TypedResults.Content(Html, "text/html; charset=utf-8", SecureEncoding.UTF8);
    }
}
