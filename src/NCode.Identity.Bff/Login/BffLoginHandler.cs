// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using Microsoft.AspNetCore.Http;
using NCode.AspNetCore;
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
using NCode.Time;

namespace NCode.Identity.Bff.Login;

/// <summary>
/// Handles <c>GET /bff/login</c>: validates the request and builds the authorization request.
/// </summary>
internal class BffLoginHandler
{
    private const string ReturnUrlParameterName = "returnUrl";
    private const string DefaultReturnUrl = "/";

    private readonly BffFrontendCatalog _frontends;
    private readonly IAuthorizationRequestClient _authorizationRequestClient;
    private readonly BffLoginTransactionService _transactionService;
    private readonly BffCookies _cookies;
    private readonly IBffAuditRecorder _auditRecorder;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="BffLoginHandler"/> class.
    /// </summary>
    public BffLoginHandler(
        BffFrontendCatalog frontends,
        IAuthorizationRequestClient authorizationRequestClient,
        BffLoginTransactionService transactionService,
        BffCookies cookies,
        IBffAuditRecorder auditRecorder,
        TimeProvider timeProvider
    )
    {
        _frontends = frontends;
        _authorizationRequestClient = authorizationRequestClient;
        _transactionService = transactionService;
        _cookies = cookies;
        _auditRecorder = auditRecorder;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Handles the login request.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    public async Task HandleAsync(HttpContext httpContext)
    {
        var request = httpContext.Request;
        var cancellationToken = httpContext.RequestAborted;
        var frontend = _frontends.GetRequired(httpContext);
        var options = frontend.Options;

        httpContext.Response.SetNoStore();

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
        if (returnUrl is null || !BffReturnUrls.IsLocalUrl(returnUrl, options.ReturnUrls))
        {
            await WriteInvalidRequestAsync(httpContext, "The returnUrl parameter is not allowed.");
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
                    $"The {name} parameter must appear once."
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
                    "The max_age parameter must be a non-negative integer."
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
        scopes.Add(OpenIdProtocol.ScopeTypes.OfflineAccess);
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
            result = await _authorizationRequestClient.CreateAuthorizationUriAsync(
                registration,
                authorizationRequest,
                cancellationToken
            );
        }
        catch (Exception exception)
            when (BffProviderFailures.IsUnavailable(exception, cancellationToken))
        {
            await FailAsync(httpContext, options, BffLoginFailureReasons.ProviderUnavailable);
            return;
        }

        if (!result.IsSuccess)
        {
            await FailAsync(
                httpContext,
                options,
                BffLoginFailureReasons.AuthorizationRequestFailed
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
            _timeProvider.GetUtcNowWithPrecisionInSeconds() + options.Session.LoginLifetime;
        await _transactionService.StoreAsync(
            state,
            initiator,
            transaction,
            expiresWhen,
            cancellationToken
        );

        _cookies.SetLogin(httpContext.Response, frontend, initiator);
        httpContext.Response.Redirect(result.AuthorizationUri.AbsoluteUri);
    }

    private async ValueTask FailAsync(
        HttpContext httpContext,
        BffFrontendOptions options,
        string reason
    )
    {
        await _auditRecorder.LoginFailedAsync(httpContext, reason, httpContext.RequestAborted);
        var errorUrl = BffNavigation.ErrorUrl(httpContext.Request, options, reason);
        httpContext.Response.Redirect(errorUrl);
    }

    private static Task WriteInvalidRequestAsync(HttpContext httpContext, string title) =>
        BffResponses.WriteProblemAsync(
            httpContext,
            StatusCodes.Status400BadRequest,
            BffProblemTypes.InvalidRequest,
            title
        );
}
