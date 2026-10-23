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

using Microsoft.Extensions.Logging;
using NCode.Identity.Cryptography;
using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;
using NCode.Time;

namespace NCode.Identity.OpenId.Authentication.Sessions;

/// <summary>
/// Provides a default implementation of the <see cref="ISessionService"/> abstraction that persists a session as a
/// <see cref="OpenIdConstants.PersistedGrantTypes.Session"/> grant keyed by its opaque <c>sid</c>.
/// </summary>
internal class DefaultSessionService(
    TimeProvider timeProvider,
    ICryptoService cryptoService,
    IPersistedGrantService persistedGrantService,
    IPersistedPayloadSerializer payloadSerializer,
    IStoreManagerFactory storeManagerFactory,
    ISessionEvictionPolicy evictionPolicy,
    IAuditEventRecorder auditEventRecorder,
    ILogger<DefaultSessionService> logger
) : ISessionService
{
    // The sliding idle window is renewed at most once per this interval, so a burst of authorize requests does not
    // amplify into a write per request; the session's last-activity marker may lag real activity by up to this amount.
    private static readonly TimeSpan TouchGranularity = TimeSpan.FromMinutes(1);

    // Page size used when enumerating a subject's sessions; a subject holds far fewer sessions than this in practice.
    private const int EnumerationPageSize = 100;

    private TimeProvider TimeProvider { get; } = timeProvider;
    private ICryptoService CryptoService { get; } = cryptoService;
    private IPersistedGrantService PersistedGrantService { get; } = persistedGrantService;
    private IPersistedPayloadSerializer PayloadSerializer { get; } = payloadSerializer;
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private ISessionEvictionPolicy EvictionPolicy { get; } = evictionPolicy;
    private IAuditEventRecorder AuditEventRecorder { get; } = auditEventRecorder;
    private ILogger<DefaultSessionService> Logger { get; } = logger;

    private PersistedGrantId CreateGrantId(OpenIdContext openIdContext, string sessionId) =>
        PersistedGrantService.CreateGrantId(
            openIdContext.Tenant.TenantId,
            OpenIdConstants.PersistedGrantTypes.Session,
            sessionId
        );

    // See ADR-0220.
    private static DateTimeOffset? GetAbsoluteEnd(
        SessionGrant payload,
        DateTimeOffset? expiresWhen
    ) => payload.AbsoluteExpiresWhen ?? expiresWhen;

    // When the session ends unless it is used again: the end of its idle window or, if earlier, its absolute end.
    private static DateTimeOffset GetEnd(SessionGrant payload, DateTimeOffset absoluteEnd) =>
        payload.IdleTimeout is { } idle && payload.LastActivityWhen + idle < absoluteEnd
            ? payload.LastActivityWhen + idle
            : absoluteEnd;

    // Why the session has ended at 'now', or null while it lasts.
    private static string? GetEndReason(
        SessionGrant payload,
        DateTimeOffset? expiresWhen,
        DateTimeOffset now
    )
    {
        if (GetAbsoluteEnd(payload, expiresWhen) is not { } absoluteEnd || now >= absoluteEnd)
            return SessionTerminationReasons.AbsoluteExpiry;

        return expiresWhen <= now || now >= GetEnd(payload, absoluteEnd)
            ? SessionTerminationReasons.IdleTimeout
            : null;
    }

    /// <inheritdoc />
    public async ValueTask<string> CreateAsync(
        OpenIdContext openIdContext,
        CreateSessionRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(request.AbsoluteLifetime, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(request.IdleTimeout, TimeSpan.Zero);

        var createdWhen = TimeProvider.GetUtcNowWithPrecisionInSeconds();

        // Enforce the concurrent-session cap before persisting the new session so that, once it is added, the subject
        // holds at most the configured number of active sessions (the oldest beyond the cap are revoked).
        await EnforceConcurrentSessionLimitAsync(
            openIdContext,
            request.SubjectId,
            request.MaxConcurrentSessions,
            cancellationToken
        );

        var sessionId = CryptoService.GenerateUrlSafeKey();
        var grantId = CreateGrantId(openIdContext, sessionId);

        var payload = new SessionGrant
        {
            AuthTime = request.AuthTime,
            AuthenticationMethods = request.AuthenticationMethods,
            AuthenticationContextClassReference = request.AuthenticationContextClassReference,
            IdentityProvider = request.IdentityProvider,
            ClientIds = [],
            LastActivityWhen = createdWhen,
            IdleTimeout = request.IdleTimeout,
            AbsoluteExpiresWhen = createdWhen + request.AbsoluteLifetime,
            UserAgent = request.UserAgent,
            RemoteIpAddress = request.RemoteIpAddress,
        };

        var grant = new PersistedGrant<SessionGrant>
        {
            Status = PersistedGrantStatus.Active,
            TenantId = openIdContext.Tenant.TenantId,
            ClientId = null,
            SubjectId = request.SubjectId,
            // The session grant carries its own sid so a session-scoped logout revokes it alongside the grants
            // issued under it in a single pass.
            SessionId = sessionId,
            Payload = payload,
        };

        // The grant expires when the session ends unless it is used again: at the end of its idle window or, if
        // earlier, at its absolute end. See ADR-0220.
        await PersistedGrantService.AddAsync(
            openIdContext,
            grantId,
            grant,
            createdWhen,
            request.IdleTimeout < request.AbsoluteLifetime
                ? request.IdleTimeout
                : request.AbsoluteLifetime,
            cancellationToken
        );

        await AuditEventRecorder.RecordSessionCreatedAsync(
            openIdContext,
            request.SubjectId,
            sessionId,
            cancellationToken
        );
        Logger.SessionCreated(sessionId, request.SubjectId);

        return sessionId;
    }

    private async ValueTask EnforceConcurrentSessionLimitAsync(
        OpenIdContext openIdContext,
        string subjectId,
        int? maxConcurrentSessions,
        CancellationToken cancellationToken
    )
    {
        if (maxConcurrentSessions is not { } max || max <= 0)
            return;

        var sessions = await GetSessionsAsync(openIdContext, subjectId, cancellationToken);
        if (sessions.Count < max)
            return;

        foreach (var session in EvictionPolicy.SelectSessionsToEvict(sessions, max))
        {
            await TerminateAsync(
                openIdContext,
                SessionTerminationReasons.ConcurrentSessionLimit,
                session.SubjectId,
                session.SessionId,
                session.Grant.ClientIds,
                cancellationToken
            );
            Logger.SessionEvicted(session.SessionId);
        }
    }

    // Dispatches the session-termination side-effect seam (grant revocation, back-channel logout, auditing, and any
    // application extensions) so every termination path is handled consistently.
    private static ValueTask TerminateAsync(
        OpenIdContext openIdContext,
        string reason,
        string? subjectId,
        string? sessionId,
        IReadOnlyList<string> participatingClientIds,
        CancellationToken cancellationToken
    ) =>
        openIdContext.Mediator.SendAsync(
            new OnSessionTerminatedCommand(
                openIdContext,
                new SessionTermination(reason, subjectId, sessionId, participatingClientIds)
            ),
            cancellationToken
        );

    /// <inheritdoc />
    public ValueTask<PersistedGrant<SessionGrant>?> GetOrDefaultAsync(
        OpenIdContext openIdContext,
        string sessionId,
        CancellationToken cancellationToken
    )
    {
        var grantId = CreateGrantId(openIdContext, sessionId);
        return PersistedGrantService.GetOrDefaultAsync<SessionGrant>(
            openIdContext,
            grantId,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async ValueTask<bool> AddClientAsync(
        OpenIdContext openIdContext,
        string sessionId,
        string clientId,
        CancellationToken cancellationToken
    )
    {
        var grantId = CreateGrantId(openIdContext, sessionId);

        var grant = await PersistedGrantService.GetOrDefaultAsync<SessionGrant>(
            openIdContext,
            grantId,
            cancellationToken
        );

        if (grant is not { Status: PersistedGrantStatus.Active } active)
            return false;

        var payload = active.Payload;
        if (payload.ClientIds.Contains(clientId, StringComparer.Ordinal))
            return true;

        var updatedPayload = payload with { ClientIds = [.. payload.ClientIds, clientId] };

        return await PersistedGrantService.UpdatePayloadAsync(
            openIdContext,
            grantId,
            updatedPayload,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async ValueTask<bool> ValidateAndTouchAsync(
        OpenIdContext openIdContext,
        string sessionId,
        CancellationToken cancellationToken
    )
    {
        var grantId = CreateGrantId(openIdContext, sessionId);

        var grant = await PersistedGrantService.GetOrDefaultAsync<SessionGrant>(
            openIdContext,
            grantId,
            cancellationToken
        );

        if (grant is not { } session || session.Status == PersistedGrantStatus.Revoked)
            return false;

        var payload = session.Payload;
        var now = TimeProvider.GetUtcNowWithPrecisionInSeconds();

        // A session that has ended, at its absolute end or at the end of its idle window, is terminated through the
        // termination seam (revoke its grants, notify its relying parties, audit) whichever bound ended it, and the
        // request has no valid session. See ADR-0220.
        if (GetEndReason(payload, session.ExpiresWhen, now) is { } endReason)
        {
            await TerminateAsync(
                openIdContext,
                endReason,
                session.SubjectId,
                sessionId,
                payload.ClientIds,
                cancellationToken
            );

            if (
                string.Equals(
                    endReason,
                    SessionTerminationReasons.AbsoluteExpiry,
                    StringComparison.Ordinal
                )
            )
                Logger.SessionAbsoluteExpired(sessionId);
            else
                Logger.SessionIdleExpired(sessionId);

            return false;
        }

        // Renew the sliding window and the grant's expiry together, throttled so a burst of requests does not write on
        // every one.
        if (
            now - payload.LastActivityWhen >= TouchGranularity
            && GetAbsoluteEnd(payload, session.ExpiresWhen) is { } absoluteEnd
        )
        {
            var touched = payload with
            {
                LastActivityWhen = now,
                AbsoluteExpiresWhen = absoluteEnd,
            };

            // A touch that finds the grant changed since the service read it is dropped, and the other change stands.
            // The touch writes the payload read above, so a write between the two reads is overwritten (backlog R91).
            // See ADR-0220.
            await PersistedGrantService.UpdatePayloadAndExpirationAsync(
                openIdContext,
                grantId,
                touched,
                GetEnd(touched, absoluteEnd),
                cancellationToken
            );
        }

        return true;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<SessionInfo>> GetSessionsAsync(
        OpenIdContext openIdContext,
        string subjectId,
        CancellationToken cancellationToken
    )
    {
        var now = TimeProvider.GetUtcNowWithPrecisionInSeconds();
        var jsonOptions = openIdContext.Environment.JsonSerializerOptions;

        var results = new List<SessionInfo>();

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IGrantStore>();

        string? cursor = null;
        do
        {
            var page = await store.GetPageAsync(
                new GrantFilter
                {
                    SubjectId = subjectId,
                    GrantType = OpenIdConstants.PersistedGrantTypes.Session,
                },
                new PageRequest { Cursor = cursor, Limit = EnumerationPageSize },
                cancellationToken
            );

            foreach (var envelope in page.Items)
            {
                if (envelope.SessionId is not { Length: > 0 } sessionId)
                    continue;

                // Skip sessions that are no longer usable: revoked or ended.
                if (envelope.RevokedWhen is not null)
                    continue;

                var payload = PayloadSerializer.Deserialize<SessionGrant>(
                    envelope.PayloadJson,
                    jsonOptions
                );

                if (
                    GetAbsoluteEnd(payload, envelope.ExpiresWhen) is not { } absoluteEnd
                    || GetEndReason(payload, envelope.ExpiresWhen, now) is not null
                )
                {
                    continue;
                }

                results.Add(
                    new SessionInfo(
                        sessionId,
                        envelope.SubjectId,
                        envelope.CreatedWhen,
                        GetEnd(payload, absoluteEnd),
                        payload
                    )
                );
            }

            cursor = page.NextCursor;
        } while (cursor is not null);

        // Newest first so the concurrent-session cap keeps the most recent sessions and the listing surfaces them in a
        // natural order.
        results.Sort((left, right) => right.CreatedWhen.CompareTo(left.CreatedWhen));

        return results;
    }

    /// <inheritdoc />
    public async ValueTask RevokeAsync(
        OpenIdContext openIdContext,
        string sessionId,
        string reason,
        CancellationToken cancellationToken
    )
    {
        // Resolve the participating relying parties so the termination seam can notify them via back-channel logout.
        var session = await GetOrDefaultAsync(openIdContext, sessionId, cancellationToken);
        var participatingClientIds = session?.Payload.ClientIds ?? [];
        var subjectId = session?.SubjectId;

        await TerminateAsync(
            openIdContext,
            reason,
            subjectId,
            sessionId,
            participatingClientIds,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async ValueTask RevokeAllAsync(
        OpenIdContext openIdContext,
        string subjectId,
        string reason,
        CancellationToken cancellationToken
    )
    {
        // Union the relying parties across all of the subject's sessions so every participant is notified once.
        var sessions = await GetSessionsAsync(openIdContext, subjectId, cancellationToken);
        var participatingClientIds = sessions
            .SelectMany(session => session.Grant.ClientIds)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // A null session id denotes a subject-wide termination (revokes every grant the subject holds).
        await TerminateAsync(
            openIdContext,
            reason,
            subjectId,
            sessionId: null,
            participatingClientIds,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async ValueTask RevokeAllExceptAsync(
        OpenIdContext openIdContext,
        string subjectId,
        string exceptSessionId,
        string reason,
        CancellationToken cancellationToken
    )
    {
        // Terminate each other session individually (not subject-wide) so the retained session and the grants issued
        // under it survive while every other session is revoked and its relying parties are notified.
        var sessions = await GetSessionsAsync(openIdContext, subjectId, cancellationToken);
        foreach (var session in sessions)
        {
            if (string.Equals(session.SessionId, exceptSessionId, StringComparison.Ordinal))
                continue;

            await TerminateAsync(
                openIdContext,
                reason,
                session.SubjectId,
                session.SessionId,
                session.Grant.ClientIds,
                cancellationToken
            );
        }
    }
}
