// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using Microsoft.AspNetCore.Http;
using NCode.Identity.AspNetCore;
using NCode.Identity.Bff.Auditing;
using NCode.Identity.Bff.Client;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Navigation;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Responses;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Client;
using NCode.Identity.Time;

namespace NCode.Identity.Bff.Login;

/// <summary>
/// Handles <c>GET /bff/login</c>: validates the request and builds the authorization request.
/// </summary>
internal class BffLoginHandler(
    BffFrontendCatalog frontends,
    IAuthorizationRequestClient authorizationRequestClient,
    BffLoginTransactionService transactionService,
    BffCookies cookies,
    IBffAuditRecorder auditRecorder,
    TimeProvider timeProvider
)
{
    private const string ReturnUrlParameterName = "returnUrl";
    private const string DefaultReturnUrl = "/";

    private BffFrontendCatalog Frontends { get; } = frontends;
    private IAuthorizationRequestClient AuthorizationRequestClient { get; } =
        authorizationRequestClient;
    private BffLoginTransactionService TransactionService { get; } = transactionService;
    private BffCookies Cookies { get; } = cookies;
    private IBffAuditRecorder AuditRecorder { get; } = auditRecorder;
    private TimeProvider TimeProvider { get; } = timeProvider;

    /// <summary>
    /// Handles the login request.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async ValueTask HandleAsync(HttpContext httpContext, CancellationToken cancellationToken)
    {
        var request = httpContext.Request;
        var frontend = Frontends.GetRequired(httpContext);
        var options = frontend.Options;

        httpContext.Response.SetNoStore();

        var returnUrlValues = request.Query[ReturnUrlParameterName];
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
        if (returnUrl is null || !BffReturnUrls.IsLocalUrl(returnUrl, options.ReturnUrls))
        {
            await WriteInvalidRequestAsync(
                httpContext,
                "The returnUrl parameter is not allowed.",
                cancellationToken
            );
            return;
        }

        var additionalParameters = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in options.LoginParameters)
        {
            var values = request.Query[name];
            if (values.Count == 0)
                continue;

            var value = values[0];
            if (values.Count > 1 || value is null)
            {
                await WriteInvalidRequestAsync(
                    httpContext,
                    $"The {name} parameter must appear once.",
                    cancellationToken
                );
                return;
            }

            additionalParameters[name] = value;
        }

        int? parsedMaxAge = null;
        if (additionalParameters.TryGetValue(OpenIdProtocol.Parameters.MaxAge, out var maxAge))
        {
            var isValidMaxAge =
                int.TryParse(
                    maxAge,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var maxAgeSeconds
                )
                && maxAgeSeconds >= 0;
            if (!isValidMaxAge)
            {
                await WriteInvalidRequestAsync(
                    httpContext,
                    "The max_age parameter must be a non-negative integer.",
                    cancellationToken
                );
                return;
            }

            parsedMaxAge = maxAgeSeconds;
        }

        var redirectUri = BffNavigation.CallbackUri(options);

        var state = BffKeyDerivation.CreateSecret();
        var initiator = BffKeyDerivation.CreateSecret();
        var verifier = BffKeyDerivation.CreateSecret();
        var nonce = BffKeyDerivation.CreateSecret();

        var scopes = new List<string> { OpenIdProtocol.ScopeTypes.OpenId };
        scopes.AddRange(options.Scopes);
        var scope = string.Join(' ', scopes.Distinct(StringComparer.Ordinal));

        var authorizationRequest = new AuthorizationRequest(redirectUri, state, verifier)
        {
            Nonce = nonce,
            Scope = scope,
            ResponseMode = OpenIdProtocol.ResponseModes.Query,
            Resources = [.. options.Resources],
            AdditionalParameters = additionalParameters.Count == 0 ? null : additionalParameters,
        };

        var registration = frontend.Registration;
        AuthorizationRequestResult result;
        try
        {
            result = await AuthorizationRequestClient.CreateAuthorizationUriAsync(
                registration,
                authorizationRequest,
                cancellationToken
            );
        }
        catch (Exception exception)
            when (BffProviderFailures.IsUnavailable(exception, cancellationToken))
        {
            await FailAsync(
                httpContext,
                options,
                BffLoginFailureReasons.ProviderUnavailable,
                cancellationToken
            );
            return;
        }

        if (!result.IsSuccess)
        {
            await FailAsync(
                httpContext,
                options,
                BffLoginFailureReasons.AuthorizationRequestFailed,
                cancellationToken
            );
            return;
        }

        var transaction = new BffLoginTransaction
        {
            Frontend = frontend.Name,
            Issuer = registration.Authority,
            RedirectUri = redirectUri,
            CodeVerifier = verifier,
            Nonce = nonce,
            ReturnUrl = returnUrl,
            DPoPJwkThumbprint = result.DPoPJwkThumbprint,
            MaxAge = parsedMaxAge,
        };
        var expiresWhen =
            TimeProvider.GetUtcNowWithPrecisionInSeconds() + options.Session.LoginLifetime;
        await TransactionService.StoreAsync(
            state,
            initiator,
            transaction,
            expiresWhen,
            cancellationToken
        );

        Cookies.SetLogin(httpContext.Response, frontend, initiator);
        httpContext.Response.Redirect(result.AuthorizationUri.AbsoluteUri);
    }

    private async ValueTask FailAsync(
        HttpContext httpContext,
        BffFrontendOptions options,
        string reason,
        CancellationToken cancellationToken
    )
    {
        await AuditRecorder.LoginFailedAsync(httpContext, reason, cancellationToken);
        var errorUrl = BffNavigation.ErrorUrl(httpContext.Request, options, reason);
        httpContext.Response.Redirect(errorUrl);
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
