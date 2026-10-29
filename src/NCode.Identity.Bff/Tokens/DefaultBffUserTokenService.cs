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
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.OpenId.Client;

namespace NCode.Identity.Bff.Tokens;

/// <summary>
/// The default <see cref="IBffUserTokenService"/>, which serves cached tokens and refreshes stale ones.
/// </summary>
internal class DefaultBffUserTokenService(
    BffSessionResolver sessionResolver,
    IBffSessionManager sessionManager,
    IBffSessionStore sessionStore,
    ITokenClient tokenClient,
    BffClientRegistrationProvider registrationProvider,
    BffRefreshLocks refreshLocks,
    BffCookies cookies,
    IBffAuditRecorder auditRecorder,
    IOptions<BffOptions> options,
    TimeProvider timeProvider,
    ILogger<DefaultBffUserTokenService> logger
) : IBffUserTokenService
{
    private readonly BffSessionResolver _sessionResolver = sessionResolver;
    private readonly IBffSessionManager _sessionManager = sessionManager;
    private readonly IBffSessionStore _sessionStore = sessionStore;
    private readonly ITokenClient _tokenClient = tokenClient;
    private readonly BffClientRegistrationProvider _registrationProvider = registrationProvider;
    private readonly BffRefreshLocks _refreshLocks = refreshLocks;
    private readonly BffCookies _cookies = cookies;
    private readonly IBffAuditRecorder _auditRecorder = auditRecorder;
    private readonly IOptions<BffOptions> _options = options;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ILogger<DefaultBffUserTokenService> _logger = logger;

    /// <inheritdoc />
    public async ValueTask<BffTokenResult> GetAccessTokenAsync(
        HttpContext httpContext,
        string? resource,
        string? scope,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var state = await _sessionResolver.ResolveAsync(httpContext, cancellationToken);
        if (state is null)
            return BffTokenResult.Failure(401, BffTokenFailureReasons.SessionRequired);

        var now = _timeProvider.GetUtcNow();
        var refreshBeforeExpiry = _options.Value.Tokens.RefreshBeforeExpiry;
        if (
            BffTokenCache.TryGetFresh(
                state.Payload,
                BffTokenRequirementNames.User,
                resource,
                scope,
                now,
                refreshBeforeExpiry,
                out var cached
            )
        )
        {
            var token = new BffAccessToken(cached.Value, cached.TokenType, cached.ExpiresAt)
            {
                DPoPJwkThumbprint = cached.DPoPJwkThumbprint,
            };
            return BffTokenResult.Success(token);
        }

        if (state.Payload.RefreshToken is null)
        {
            await _sessionManager.EndAsync(
                httpContext,
                state.StoreKey,
                state.Metadata,
                BffSessionEndReasons.RefreshFailed,
                CancellationToken.None
            );
            _cookies.DeleteSession(httpContext.Response);
            return BffTokenResult.Failure(401, BffTokenFailureReasons.RefreshFailed);
        }

        return await RefreshAsync(httpContext, state, resource, scope, cancellationToken);
    }

    // step 47 implements the refresh under the cluster-wide lease
    private ValueTask<BffTokenResult> RefreshAsync(
        HttpContext httpContext,
        BffSessionState state,
        string? resource,
        string? scope,
        CancellationToken cancellationToken
    ) => throw new NotImplementedException();
}
