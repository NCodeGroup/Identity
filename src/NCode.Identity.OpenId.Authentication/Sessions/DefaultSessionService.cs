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

using NCode.Identity.Jose.Extensions;
using NCode.Identity.Logic;
using NCode.Identity.OpenId.Authentication.Logic;
using NCode.Identity.OpenId.Authentication.Models;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Sessions;

/// <summary>
/// Provides a default implementation of the <see cref="ISessionService"/> abstraction that persists a session as a
/// <see cref="OpenIdConstants.PersistedGrantTypes.Session"/> grant keyed by its opaque <c>sid</c>.
/// </summary>
internal class DefaultSessionService(
    TimeProvider timeProvider,
    ICryptoService cryptoService,
    IPersistedGrantService persistedGrantService
) : ISessionService
{
    private TimeProvider TimeProvider { get; } = timeProvider;
    private ICryptoService CryptoService { get; } = cryptoService;
    private IPersistedGrantService PersistedGrantService { get; } = persistedGrantService;

    private PersistedGrantId CreateGrantId(OpenIdContext openIdContext, string sessionId) =>
        PersistedGrantService.CreateGrantId(
            openIdContext.Tenant.TenantId,
            OpenIdConstants.PersistedGrantTypes.Session,
            sessionId
        );

    /// <inheritdoc />
    public async ValueTask<string> CreateAsync(
        OpenIdContext openIdContext,
        CreateSessionRequest request,
        CancellationToken cancellationToken
    )
    {
        var sessionId = CryptoService.GenerateUrlSafeKey();
        var grantId = CreateGrantId(openIdContext, sessionId);

        var payload = new SessionGrant
        {
            AuthTime = request.AuthTime,
            AuthenticationMethods = request.AuthenticationMethods,
            IdentityProvider = request.IdentityProvider,
            ClientIds = [],
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

        var createdWhen = TimeProvider.GetUtcNowWithPrecisionInSeconds();

        await PersistedGrantService.AddAsync(
            openIdContext,
            grantId,
            grant,
            createdWhen,
            request.Lifetime,
            cancellationToken
        );

        return sessionId;
    }

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
}
