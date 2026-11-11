// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using NCode.Identity.AspNetCore;
using NCode.Identity.Bff.Client;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Navigation;
using NCode.Identity.Bff.Responses;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Client;

namespace NCode.Identity.Bff.Logout;

/// <summary>
/// Handles <c>POST /bff/logout</c>: checks the request and ends the session of the user.
/// </summary>
internal class BffLogoutHandler(
    BffFrontendCatalog frontends,
    BffSessionResolver resolver,
    BffLogoutService logoutService,
    IProviderMetadataResolver metadataResolver
)
{
    private const string ReturnUrlParameterName = "returnUrl";
    private const string SidParameterName = "sid";
    private const string DefaultReturnUrl = "/";

    private BffFrontendCatalog Frontends { get; } = frontends;
    private BffSessionResolver Resolver { get; } = resolver;
    private BffLogoutService LogoutService { get; } = logoutService;
    private IProviderMetadataResolver MetadataResolver { get; } = metadataResolver;

    /// <summary>
    /// Handles the logout request.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public virtual async ValueTask HandleAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var request = httpContext.Request;
        var frontend = Frontends.GetRequired(httpContext);
        var bffOptions = frontend.Options;

        httpContext.Response.SetNoStore();

        if (!request.HasFormContentType)
        {
            await WriteInvalidRequestAsync(
                httpContext,
                "The logout request must be a form post.",
                cancellationToken
            );
            return;
        }

        var form = await request.TryReadFormAsync(cancellationToken);
        if (form is null)
        {
            await WriteInvalidRequestAsync(
                httpContext,
                "The logout request is not a valid form post.",
                cancellationToken
            );
            return;
        }

        var returnUrlValues = form[ReturnUrlParameterName];
        if (returnUrlValues.Count > 1)
        {
            await WriteInvalidRequestAsync(
                httpContext,
                "The returnUrl parameter must appear once.",
                cancellationToken
            );
            return;
        }

        var returnUrl = returnUrlValues.Count == 0 ? DefaultReturnUrl : returnUrlValues[0];
        if (returnUrl is null || !BffReturnUrls.IsLocalUrl(returnUrl, bffOptions.ReturnUrls))
        {
            await WriteInvalidRequestAsync(
                httpContext,
                "The returnUrl parameter is not allowed.",
                cancellationToken
            );
            return;
        }

        var session = await Resolver.ResolveAsync(httpContext, cancellationToken);
        if (session is null)
        {
            httpContext.Response.Redirect(BffNavigation.LocalUrl(request, returnUrl));
            return;
        }

        var sidValues = form[SidParameterName];
        var sid = sidValues.Count == 1 ? sidValues[0] : null;
        if (sid is null || !IsGuardMatch(sid, session.Payload.LogoutGuard))
        {
            await WriteInvalidRequestAsync(
                httpContext,
                "The sid parameter is missing or invalid.",
                cancellationToken
            );
            return;
        }

        var idToken = session.Payload.IdToken;

        await LogoutService.EndSessionAsync(httpContext, session, BffSessionEndReasons.LoggedOut);

        var registration = frontend.Registration;

        OpenIdProviderMetadata metadata;
        try
        {
            metadata = await MetadataResolver.GetMetadataAsync(
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

        var postLogoutRedirectUri = BffNavigation.AbsoluteUrl(bffOptions, returnUrl);
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
        return CryptographicOperations.FixedTimeEquals(
            MemoryMarshal.AsBytes(sid.AsSpan()),
            MemoryMarshal.AsBytes(guard.AsSpan())
        );
    }

    private static ValueTask WriteInvalidRequestAsync(
        HttpContext httpContext,
        string title,
        CancellationToken cancellationToken
    ) =>
        BffResponses.WriteProblemAsync(
            httpContext,
            StatusCodes.Status400BadRequest,
            BffProblemTypes.InvalidRequest,
            title,
            cancellationToken
        );
}
