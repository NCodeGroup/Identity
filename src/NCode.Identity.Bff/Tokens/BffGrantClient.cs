// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NCode.Identity.Bff.Client;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Logging;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Client;
using NCode.Time;

namespace NCode.Identity.Bff.Tokens;

/// <summary>
/// Requests an access token from the provider's token endpoint as the BFF's client registration, for the grants that
/// issue no refresh token: <c>client_credentials</c> and token exchange.
/// </summary>
internal class BffGrantClient(
    ITokenClient tokenClient,
    TimeProvider timeProvider,
    ILogger<BffGrantClient> logger
)
{
    private readonly ITokenClient _tokenClient = tokenClient;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ILogger<BffGrantClient> _logger = logger;

    /// <summary>
    /// Requests an access token as the frontend's client registration, answering <c>503</c> when the provider cannot
    /// be reached and <c>502</c> when it refuses the request or answers without an access token.
    /// </summary>
    /// <param name="frontend">The frontend whose client registration makes the request.</param>
    /// <param name="request">The token request.</param>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    /// <returns>The issued access token, or the failure.</returns>
    public virtual ValueTask<BffTokenResult> RequestAsync(
        BffFrontend frontend,
        TokenRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(frontend);
        ArgumentNullException.ThrowIfNull(request);

        return RequestWithRegistrationAsync(frontend.Registration, request, cancellationToken);
    }

    private async ValueTask<BffTokenResult> RequestWithRegistrationAsync(
        ClientRegistration registration,
        TokenRequest request,
        CancellationToken cancellationToken
    )
    {
        var now = _timeProvider.GetUtcNowWithPrecisionInSeconds();
        TokenResult result;
        try
        {
            result = await _tokenClient.RequestTokenAsync(registration, request, cancellationToken);
        }
        catch (Exception exception)
            when (BffProviderFailures.IsUnavailable(exception, cancellationToken))
        {
            _logger.TokenRequestFailed(
                request.GrantType,
                BffTokenFailureReasons.ProviderUnavailable,
                exception
            );
            return BffTokenResult.Failure(
                StatusCodes.Status503ServiceUnavailable,
                BffTokenFailureReasons.ProviderUnavailable
            );
        }

        if (!result.IsSuccess)
        {
            _logger.TokenRequestFailed(request.GrantType, result.Error.Error, null);
            return BffTokenResult.Failure(
                StatusCodes.Status502BadGateway,
                BffTokenFailureReasons.ProviderError
            );
        }

        var response = result.Response;
        var accessToken = response.AccessToken;
        if (string.IsNullOrEmpty(accessToken))
        {
            _logger.TokenRequestFailed(
                request.GrantType,
                BffTokenFailureReasons.ProviderError,
                null
            );
            return BffTokenResult.Failure(
                StatusCodes.Status502BadGateway,
                BffTokenFailureReasons.ProviderError
            );
        }

        var expiresAt = response.ExpiresIn is { } expiresIn
            ? now + TimeSpan.FromSeconds(expiresIn)
            : now;
        var tokenType = string.IsNullOrEmpty(response.TokenType)
            ? OpenIdProtocol.TokenTypes.Bearer
            : response.TokenType;
        var token = new BffAccessToken(accessToken, tokenType, expiresAt)
        {
            DPoPJwkThumbprint = result.DPoPJwkThumbprint,
        };
        return BffTokenResult.Success(token);
    }
}
