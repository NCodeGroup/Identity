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

using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using NCode.Buffers;
using NCode.Identity.Bff.Client;
using NCode.Identity.Bff.Navigation;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Responses;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Client;

namespace NCode.Identity.Bff.Logout;

/// <summary>
/// Handles <c>GET /bff/logout</c>: checks the request and ends the session of the user.
/// </summary>
internal class BffLogoutHandler
{
    private const string ReturnUrlParameterName = "returnUrl";
    private const string SidParameterName = "sid";
    private const string DefaultReturnUrl = "/";

    private readonly IOptions<BffOptions> _options;
    private readonly BffSessionResolver _resolver;
    private readonly BffLogoutService _logoutService;
    private readonly IProviderMetadataResolver _metadataResolver;
    private readonly BffClientRegistrationProvider _registrationProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="BffLogoutHandler"/> class.
    /// </summary>
    public BffLogoutHandler(
        IOptions<BffOptions> options,
        BffSessionResolver resolver,
        BffLogoutService logoutService,
        IProviderMetadataResolver metadataResolver,
        BffClientRegistrationProvider registrationProvider
    )
    {
        _options = options;
        _resolver = resolver;
        _logoutService = logoutService;
        _metadataResolver = metadataResolver;
        _registrationProvider = registrationProvider;
    }

    /// <summary>
    /// Handles the logout request.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    public virtual async Task HandleAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var request = httpContext.Request;
        var cancellationToken = httpContext.RequestAborted;
        var bffOptions = _options.Value;

        BffResponses.SetNoStore(httpContext.Response);

        var returnUrlValues = request.Query[ReturnUrlParameterName];
        if (returnUrlValues.Count > 1)
        {
            await WriteInvalidRequestAsync(
                httpContext,
                "The returnUrl parameter must appear once."
            );
            return;
        }

        var returnUrl = returnUrlValues.Count == 0 ? DefaultReturnUrl : returnUrlValues[0];
        if (returnUrl is null || !BffReturnUrls.IsAllowed(returnUrl, bffOptions.ReturnUrls))
        {
            await WriteInvalidRequestAsync(httpContext, "The returnUrl parameter is not allowed.");
            return;
        }

        var session = await _resolver.ResolveAsync(httpContext, cancellationToken);
        if (session is null)
        {
            httpContext.Response.Redirect(BffNavigation.LocalUrl(request, returnUrl));
            return;
        }

        var sidValues = request.Query[SidParameterName];
        var sid = sidValues.Count == 1 ? sidValues[0] : null;
        if (sid is null || !IsGuardMatch(sid, session.Payload.LogoutGuard))
        {
            await WriteInvalidRequestAsync(httpContext, "The sid parameter is missing or invalid.");
            return;
        }

        var idToken = session.Payload.IdToken;

        await _logoutService.EndSessionAsync(
            httpContext,
            session,
            BffSessionEndReasons.LoggedOut,
            cancellationToken
        );

        var registration = _registrationProvider.Registration;

        OpenIdProviderMetadata metadata;
        try
        {
            metadata = await _metadataResolver.GetMetadataAsync(
                registration.Authority,
                cancellationToken
            );
        }
        catch (Exception exception)
            when (BffProviderFailures.IsUnavailable(exception, cancellationToken))
        {
            httpContext.Response.Redirect(BffNavigation.LocalUrl(request, returnUrl));
            return;
        }

        var endpoint = metadata.EndSessionEndpoint;
        if (endpoint is null)
        {
            httpContext.Response.Redirect(BffNavigation.LocalUrl(request, returnUrl));
            return;
        }

        var postLogoutRedirectUri = BffNavigation.AbsoluteUrl(request, returnUrl);
        var state = BffKeyDerivation.CreateSecret();
        var parameters = new Dictionary<string, string?>
        {
            [OpenIdProtocol.Parameters.IdTokenHint] = idToken,
            [OpenIdProtocol.Parameters.ClientId] = registration.ClientId,
            [OpenIdProtocol.Parameters.PostLogoutRedirectUri] = postLogoutRedirectUri,
            [OpenIdProtocol.Parameters.State] = state,
        };
        var location = QueryHelpers.AddQueryString(endpoint, parameters);
        httpContext.Response.Redirect(location);
    }

    private static bool IsGuardMatch(string sid, string guard)
    {
        var sidBytes = SecureEncoding.UTF8.GetBytes(sid);
        var guardBytes = SecureEncoding.UTF8.GetBytes(guard);
        return CryptographicOperations.FixedTimeEquals(sidBytes, guardBytes);
    }

    private static Task WriteInvalidRequestAsync(HttpContext httpContext, string title) =>
        BffResponses.WriteProblemAsync(
            httpContext,
            StatusCodes.Status400BadRequest,
            BffProblemTypes.InvalidRequest,
            title
        );
}
