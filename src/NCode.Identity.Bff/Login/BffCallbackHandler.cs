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

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.Bff.Auditing;
using NCode.Identity.Bff.Client;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Logging;
using NCode.Identity.Bff.Navigation;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Responses;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Client;

namespace NCode.Identity.Bff.Login;

/// <summary>
/// Handles <c>GET /bff/callback</c>: binds the returning browser to its login transaction.
/// </summary>
internal class BffCallbackHandler
{
    private readonly IOptions<BffOptions> _options;
    private readonly BffClientRegistrationProvider _registrationProvider;
    private readonly BffLoginTransactionService _transactionService;
    private readonly BffCookies _cookies;
    private readonly IProviderMetadataResolver _metadataResolver;
    private readonly ITokenClient _tokenClient;
    private readonly IIdTokenValidator _idTokenValidator;
    private readonly IEnumerable<IBffLoginGate> _gates;
    private readonly IBffSessionManager _sessionManager;
    private readonly IBffAuditRecorder _auditRecorder;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<BffCallbackHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BffCallbackHandler"/> class.
    /// </summary>
    public BffCallbackHandler(
        IOptions<BffOptions> options,
        BffClientRegistrationProvider registrationProvider,
        BffLoginTransactionService transactionService,
        BffCookies cookies,
        IProviderMetadataResolver metadataResolver,
        ITokenClient tokenClient,
        IIdTokenValidator idTokenValidator,
        IEnumerable<IBffLoginGate> gates,
        IBffSessionManager sessionManager,
        IBffAuditRecorder auditRecorder,
        TimeProvider timeProvider,
        ILogger<BffCallbackHandler> logger
    )
    {
        _options = options;
        _registrationProvider = registrationProvider;
        _transactionService = transactionService;
        _cookies = cookies;
        _metadataResolver = metadataResolver;
        _tokenClient = tokenClient;
        _idTokenValidator = idTokenValidator;
        _gates = gates;
        _sessionManager = sessionManager;
        _auditRecorder = auditRecorder;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Handles the callback request.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    public async Task HandleAsync(HttpContext httpContext)
    {
        var request = httpContext.Request;
        var cancellationToken = httpContext.RequestAborted;

        BffResponses.SetNoStore(httpContext.Response);
        _cookies.DeleteLogin(httpContext.Response);

        var stateValues = request.Query[OpenIdProtocol.Parameters.State];
        var state = stateValues.Count == 1 ? stateValues[0] : null;
        var loginSecret = _cookies.ReadLoginSecret(request);
        var taken = await _transactionService.TakeAsync(state, loginSecret, cancellationToken);
        if (taken.Transaction is not { } transaction)
        {
            await FailAsync(
                httpContext,
                taken.FailureReason ?? BffLoginFailureReasons.UnknownState
            );
            return;
        }

        if (request.Query.ContainsKey(OpenIdProtocol.Parameters.ErrorCode))
        {
            await FailAsync(httpContext, BffLoginFailureReasons.AuthorizationError);
            return;
        }

        var metadata = await _metadataResolver.GetMetadataAsync(
            transaction.Issuer,
            cancellationToken
        );
        var issValues = request.Query[OpenIdProtocol.Parameters.IssuerShort];
        var issMissing = issValues.Count == 0;
        var issMismatch =
            !issMissing
            && (
                issValues.Count != 1
                || !string.Equals(issValues[0], transaction.Issuer, StringComparison.Ordinal)
            );
        if (issMismatch || (issMissing && metadata.AuthorizationResponseIssParameterSupported))
        {
            await FailAsync(httpContext, BffLoginFailureReasons.IssuerMismatch);
            return;
        }

        var codeValues = request.Query[OpenIdProtocol.Parameters.AuthorizationCode];
        if (codeValues.Count != 1 || string.IsNullOrEmpty(codeValues[0]))
        {
            await FailAsync(httpContext, BffLoginFailureReasons.InvalidResponse);
            return;
        }

        // The code exchange, ID token validation and session creation follow in the next steps.
        throw new NotImplementedException();
    }

    private async ValueTask FailAsync(HttpContext httpContext, string reason)
    {
        _logger.LoginFailed(reason);
        await _auditRecorder.LoginFailedAsync(httpContext, reason, httpContext.RequestAborted);
        var errorUrl = BffNavigation.ErrorUrl(httpContext.Request, _options.Value, reason);
        httpContext.Response.Redirect(errorUrl);
    }
}
