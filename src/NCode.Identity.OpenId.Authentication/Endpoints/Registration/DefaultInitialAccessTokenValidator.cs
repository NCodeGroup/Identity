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

using NCode.Identity.OpenId.Authentication.Endpoints.Registration.Grants;
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Registration;

/// <summary>
/// Provides the default implementation of the <see cref="IInitialAccessTokenValidator"/> abstraction that treats the
/// presented initial access token as a persisted initial-access-token grant: the token authorizes registration when a
/// matching grant exists for the current tenant that is active, carries an expiry and has not been spent, and the
/// registration it authorizes spends that grant, so the token authorizes no other registration.
/// </summary>
internal class DefaultInitialAccessTokenValidator(IPersistedGrantService persistedGrantService)
    : IInitialAccessTokenValidator
{
    private IPersistedGrantService PersistedGrantService { get; } = persistedGrantService;

    /// <inheritdoc />
    public async ValueTask<InitialAccessTokenGrant?> ValidateAsync(
        OpenIdContext openIdContext,
        string initialAccessToken,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrEmpty(initialAccessToken))
            return null;

        var grantId = PersistedGrantService.CreateGrantId(
            openIdContext.Tenant.TenantId,
            OpenIdConstants.PersistedGrantTypes.InitialAccessToken,
            initialAccessToken
        );

        var grant = await PersistedGrantService.GetOrDefaultAsync<InitialAccessTokenGrant>(
            openIdContext,
            grantId,
            cancellationToken
        );

        // Every minted token expires, so a grant without an expiry was minted by an earlier release without a
        // lifetime, and it authorizes nothing. See ADR-0192.
        return
            grant
                is {
                    Status: PersistedGrantStatus.Active,
                    ExpiresWhen: not null,
                    ConsumedWhen: null
                }
            ? grant.Value.Payload
            : null;
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryConsumeAsync(
        OpenIdContext openIdContext,
        string initialAccessToken,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrEmpty(initialAccessToken))
            return false;

        var grantId = PersistedGrantService.CreateGrantId(
            openIdContext.Tenant.TenantId,
            OpenIdConstants.PersistedGrantTypes.InitialAccessToken,
            initialAccessToken
        );

        // The grant row's concurrency token makes the spend atomic across replicas. See ADR-0192.
        return await PersistedGrantService.ConsumeOnceOrDefault<InitialAccessTokenGrant>(
            openIdContext,
            grantId,
            cancellationToken
        )
            is not null;
    }
}
