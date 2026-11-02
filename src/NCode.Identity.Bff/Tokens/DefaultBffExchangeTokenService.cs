// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NCode.Identity.Bff.Options;
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
    BffSessionResolver sessionResolver,
    IBffSessionManager sessionManager,
    IBffUserTokenService userTokenService,
    BffGrantClient grantClient,
    IOptions<BffOptions> options,
    TimeProvider timeProvider
) : IBffExchangeTokenService
{
    private readonly BffSessionResolver _sessionResolver = sessionResolver;
    private readonly IBffSessionManager _sessionManager = sessionManager;
    private readonly IBffUserTokenService _userTokenService = userTokenService;
    private readonly BffGrantClient _grantClient = grantClient;
    private readonly IOptions<BffOptions> _options = options;
    private readonly TimeProvider _timeProvider = timeProvider;

    // separate from the user token service's locks, which the subject token's refresh takes inside this one
    private BffRefreshLocks Locks { get; } = new();

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

        var fresh = TryGetFresh(state.Payload, resource, scope);
        if (fresh is not null)
            return fresh;

        using var held = await Locks.TryEnterAsync(
            state.StoreKey,
            _options.Value.Tokens.RefreshLeaseDuration,
            cancellationToken
        );
        if (held is null)
            return BffTokenResult.Failure(503, BffTokenFailureReasons.RefreshWaitTimeout);

        var current = await _sessionManager.LoadAsync(state.Handle, cancellationToken);
        if (current is null)
            return BffTokenResult.Failure(401, BffTokenFailureReasons.SessionRequired);

        fresh = TryGetFresh(current.Payload, resource, scope);
        if (fresh is not null)
            return fresh;

        return await ExchangeAsync(httpContext, current, resource, scope, cancellationToken);
    }

    private async ValueTask<BffTokenResult> ExchangeAsync(
        HttpContext httpContext,
        BffSessionState current,
        string? resource,
        string? scope,
        CancellationToken cancellationToken
    )
    {
        // a failure to get the user's token, such as a refresh that ended the session, is the exchange's failure
        var subject = await _userTokenService.GetAccessTokenAsync(
            httpContext,
            null,
            null,
            cancellationToken
        );
        if (subject.Token is not { } subjectToken)
            return subject.IsSuccess
                ? BffTokenResult.Failure(401, BffTokenFailureReasons.SessionRequired)
                : subject;

        var request = new TokenExchangeTokenRequest(
            subjectToken.Value,
            OpenIdProtocol.TokenTypeIdentifiers.AccessToken
        )
        {
            Resources = resource is null ? [] : [resource],
            Scope = scope,
            DPoPJwkThumbprint = current.Payload.DPoPJwkThumbprint,
        };
        var result = await _grantClient.RequestAsync(request, cancellationToken);
        if (result.Token is not { } token)
            return result;

        var entry = new BffStoredAccessToken
        {
            Requirement = BffTokenRequirementNames.Exchange,
            Resource = resource,
            Scope = scope,
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
            ? BffTokenResult.Failure(401, BffTokenFailureReasons.SessionRequired)
            : result;
    }

    private BffTokenResult? TryGetFresh(BffSessionPayload payload, string? resource, string? scope)
    {
        if (
            !BffTokenCache.TryGetFresh(
                payload,
                BffTokenRequirementNames.Exchange,
                resource,
                scope,
                _timeProvider.GetUtcNow(),
                _options.Value.Tokens.RefreshBeforeExpiry,
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
