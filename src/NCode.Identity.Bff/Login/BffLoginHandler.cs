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

using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NCode.Identity.Bff.Auditing;
using NCode.Identity.Bff.Client;
using NCode.Identity.Bff.Cookies;
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

    private readonly IOptions<BffOptions> _options;
    private readonly BffClientRegistrationProvider _registrationProvider;
    private readonly IAuthorizationRequestClient _authorizationRequestClient;
    private readonly BffLoginTransactionService _transactionService;
    private readonly BffCookies _cookies;
    private readonly IBffAuditRecorder _auditRecorder;
    private readonly IHostEnvironment _environment;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="BffLoginHandler"/> class.
    /// </summary>
    public BffLoginHandler(
        IOptions<BffOptions> options,
        BffClientRegistrationProvider registrationProvider,
        IAuthorizationRequestClient authorizationRequestClient,
        BffLoginTransactionService transactionService,
        BffCookies cookies,
        IBffAuditRecorder auditRecorder,
        IHostEnvironment environment,
        TimeProvider timeProvider
    )
    {
        _options = options;
        _registrationProvider = registrationProvider;
        _authorizationRequestClient = authorizationRequestClient;
        _transactionService = transactionService;
        _cookies = cookies;
        _auditRecorder = auditRecorder;
        _environment = environment;
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

        var additionalParameters = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in bffOptions.LoginParameters)
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

        var redirectUri = BffNavigation.CallbackUri(request, bffOptions);
        var isHttps = redirectUri.StartsWith(Uri.UriSchemeHttps + "://", StringComparison.Ordinal);
        if (!isHttps && !_environment.IsDevelopment())
            throw new InvalidOperationException("The BFF callback URI must use https.");

        var state = BffKeyDerivation.CreateSecret();
        var initiator = BffKeyDerivation.CreateSecret();
        var verifier = BffKeyDerivation.CreateSecret();
        var nonce = BffKeyDerivation.CreateSecret();

        var scopes = new List<string> { OpenIdProtocol.ScopeTypes.OpenId };
        scopes.AddRange(bffOptions.Scopes);
        scopes.Add(OpenIdProtocol.ScopeTypes.OfflineAccess);
        var scope = string.Join(' ', scopes.Distinct(StringComparer.Ordinal));

        var authorizationRequest = new AuthorizationRequest(redirectUri, state, verifier)
        {
            Nonce = nonce,
            Scope = scope,
            ResponseMode = OpenIdProtocol.ResponseModes.Query,
            Resources = [.. bffOptions.Resources],
            AdditionalParameters = additionalParameters.Count == 0 ? null : additionalParameters,
        };

        var registration = _registrationProvider.Registration;
        AuthorizationRequestResult result;
        try
        {
            result = await _authorizationRequestClient.CreateAuthorizationUriAsync(
                registration,
                authorizationRequest,
                cancellationToken
            );
        }
        catch (OpenIdProviderException)
        {
            await FailAsync(httpContext, bffOptions);
            return;
        }

        if (!result.IsSuccess)
        {
            await FailAsync(httpContext, bffOptions);
            return;
        }

        var transaction = new BffLoginTransaction
        {
            Issuer = registration.Authority,
            RedirectUri = redirectUri,
            CodeVerifier = verifier,
            Nonce = nonce,
            ReturnUrl = returnUrl,
            DPoPJwkThumbprint = result.DPoPJwkThumbprint,
            MaxAge = parsedMaxAge,
        };
        var expiresWhen =
            _timeProvider.GetUtcNowWithPrecisionInSeconds() + bffOptions.Session.LoginLifetime;
        await _transactionService.StoreAsync(
            state,
            initiator,
            transaction,
            expiresWhen,
            cancellationToken
        );

        _cookies.SetLogin(httpContext.Response, initiator);
        httpContext.Response.Redirect(result.AuthorizationUri.AbsoluteUri);
    }

    private async ValueTask FailAsync(HttpContext httpContext, BffOptions bffOptions)
    {
        await _auditRecorder.LoginFailedAsync(
            httpContext,
            BffLoginFailureReasons.AuthorizationRequestFailed,
            httpContext.RequestAborted
        );
        var errorUrl = BffNavigation.ErrorUrl(
            httpContext.Request,
            bffOptions,
            BffLoginFailureReasons.AuthorizationRequestFailed
        );
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
