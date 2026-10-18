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
/// matching, still-active grant exists for the current tenant.
/// </summary>
internal class DefaultInitialAccessTokenValidator(IPersistedGrantService persistedGrantService)
    : IInitialAccessTokenValidator
{
    private IPersistedGrantService PersistedGrantService { get; } = persistedGrantService;

    /// <inheritdoc />
    public async ValueTask<bool> ValidateAsync(
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

        var grant = await PersistedGrantService.GetOrDefaultAsync<InitialAccessTokenGrant>(
            openIdContext,
            grantId,
            cancellationToken
        );

        return grant is { Status: PersistedGrantStatus.Active };
    }
}
