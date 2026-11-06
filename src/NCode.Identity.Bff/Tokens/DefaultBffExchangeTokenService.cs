// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Client;

namespace NCode.Identity.Bff.Tokens;

/// <summary>
/// The default <see cref="IBffExchangeTokenService"/>, which exchanges the user's access token for one per resource
/// and scope and caches the result in the session.
/// </summary>
// Token exchange issues no refresh token and presents nothing that a second exchange could not present again, so
// replicas need no lease: a lock inside the replica only keeps one session from exchanging twice at once. See ADR-1381.
internal class DefaultBffExchangeTokenService(
    IBffSessionManager sessionManager,
    IBffUserTokenService userTokenService,
    BffGrantClient grantClient,
    BffRefreshLocks locks,
    BffFrontendCatalog frontends,
    TimeProvider timeProvider
) : IBffExchangeTokenService
{
    private readonly IBffSessionManager _sessionManager = sessionManager;
    private readonly IBffUserTokenService _userTokenService = userTokenService;
    private readonly BffGrantClient _grantClient = grantClient;
    private readonly BffRefreshLocks _locks = locks;
    private readonly BffFrontendCatalog _frontends = frontends;
    private readonly TimeProvider _timeProvider = timeProvider;

    // the subject token is the user's token for the provider's default resource and scope
    private static BffTokenRequest SubjectTokenRequest { get; } =
        new(BffTokenRequirementNames.User);

    /// <inheritdoc />
    public async ValueTask<BffTokenResult> GetAccessTokenAsync(
        IBffSessionScope scope,
        BffTokenRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(request);

        var state = await scope.ResolveAsync(cancellationToken);
        if (state is null)
            return BffTokenResult.Failure(
                StatusCodes.Status401Unauthorized,
                BffTokenFailureReasons.SessionRequired
            );

        var fresh = TryGetFresh(state.Payload, request);
        if (fresh is not null)
            return fresh;

        // the subject token's refresh takes the user token service's lock on the same session inside this one
        using var held = await _locks.TryEnterAsync(
            BffRefreshLocks.ExchangePurpose,
            state.StoreKey,
            _frontends.Current.Options.Tokens.RefreshLeaseDuration,
            cancellationToken
        );
        if (held is null)
            return BffTokenResult.Failure(
                StatusCodes.Status503ServiceUnavailable,
                BffTokenFailureReasons.RefreshWaitTimeout
            );

        var current = await _sessionManager.LoadAsync(state.Handle, cancellationToken);
        if (current is null)
            return BffTokenResult.Failure(
                StatusCodes.Status401Unauthorized,
                BffTokenFailureReasons.SessionRequired
            );

        fresh = TryGetFresh(current.Payload, request);
        if (fresh is not null)
            return fresh;

        return await ExchangeAsync(scope, current, request, cancellationToken);
    }

    private async ValueTask<BffTokenResult> ExchangeAsync(
        IBffSessionScope scope,
        BffSessionState current,
        BffTokenRequest request,
        CancellationToken cancellationToken
    )
    {
        // a failure to get the user's token, such as a refresh that ended the session, is the exchange's failure
        var subject = await _userTokenService.GetAccessTokenAsync(
            scope,
            SubjectTokenRequest,
            cancellationToken
        );
        if (subject.Token is not { } subjectToken)
            return subject.IsSuccess
                ? BffTokenResult.Failure(
                    StatusCodes.Status401Unauthorized,
                    BffTokenFailureReasons.SessionRequired
                )
                : subject;

        var grantRequest = new TokenExchangeTokenRequest(
            subjectToken.Value,
            OpenIdProtocol.TokenTypeIdentifiers.AccessToken
        )
        {
            Resources = request.Resource is null ? [] : [request.Resource],
            Scope = request.Scope,
            DPoPJwkThumbprint = current.Payload.DPoPJwkThumbprint,
        };
        var frontend = scope.RequireFrontend();
        var result = await _grantClient.RequestAsync(frontend, grantRequest, cancellationToken);
        if (result.Token is not { } token)
            return result;

        var entry = new BffStoredAccessToken
        {
            Requirement = BffTokenRequirementNames.Exchange,
            Resource = request.Resource,
            Scope = request.Scope,
            Value = token.Value,
            TokenType = token.TokenType,
            ExpiresAt = token.ExpiresAt,
            DPoPJwkThumbprint = token.DPoPJwkThumbprint,
        };

        // a lost write is merged onto the newer version, such as the one the subject token's refresh wrote
        var updated = await _sessionManager.UpdateAsync(
            current,
            session => session with { Payload = BffTokenCache.WithToken(session.Payload, entry) },
            CancellationToken.None
        );
        return updated is null
            ? BffTokenResult.Failure(
                StatusCodes.Status401Unauthorized,
                BffTokenFailureReasons.SessionRequired
            )
            : result;
    }

    private BffTokenResult? TryGetFresh(BffSessionPayload payload, BffTokenRequest request)
    {
        if (
            !BffTokenCache.TryGetFresh(
                payload,
                BffTokenRequirementNames.Exchange,
                request.Resource,
                request.Scope,
                _timeProvider.GetUtcNow(),
                _frontends.Current.Options.Tokens.RefreshBeforeExpiry,
                out var cached
            )
        )
            return null;

        var token = new BffAccessToken(cached.Value, cached.TokenType, cached.ExpiresAt)
        {
            DPoPJwkThumbprint = cached.DPoPJwkThumbprint,
        };
        return BffTokenResult.Success(token);
    }
}
