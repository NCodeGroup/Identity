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

using System.Collections.Frozen;
using System.Security.Claims;
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Identity.OpenId.Authentication.Endpoints.UserInfo.Commands;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Mediator;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Accounts.Authentication;

/// <summary>
/// Provides a <see cref="GetUserInfoClaimsCommand"/> handler that returns the authenticated subject's
/// <c>profile</c>/<c>email</c>/<c>address</c>/<c>phone</c> scope claims from the configured
/// <see cref="ILocalAccountStore"/>. Per OpenID Connect Core 1.0 §5.4 these scope-driven claims are returned from the
/// UserInfo endpoint (not the ID token) whenever an access token was issued, so the matching gate in the ID-token
/// subject-claims handler keeps them out of the ID token and this handler supplies them here. The local account is
/// resolved from the caller's principal via its one-to-one self-issued connection
/// (<see cref="AccountConstants.SelfIssuer"/>), and only the claims permitted by the token's granted scopes are
/// emitted. A host fronting a different user store registers its own enricher instead.
/// </summary>
// See ADR-0030, ADR-0051.
internal sealed class DefaultGetLocalAccountUserInfoClaimsHandler(
    IStoreManagerFactory storeManagerFactory
) : ICommandHandler<GetUserInfoClaimsCommand>, ISupportMediatorPriority
{
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;

    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.Low;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        GetUserInfoClaimsCommand command,
        CancellationToken cancellationToken
    )
    {
        var (_, subjectAuthentication, claims) = command;

        var scopes = GetGrantedScopes(subjectAuthentication.Subject);
        if (scopes.Count == 0)
        {
            return;
        }

        var wantsEmail = scopes.Contains(OpenIdConstants.ScopeTypes.Email);
        var claimTypes = CollectScopeClaimTypes(scopes);
        if (!wantsEmail && claimTypes.Count == 0)
        {
            return;
        }

        var account = await ResolveLocalAccountAsync(
            subjectAuthentication.PrincipalId,
            cancellationToken
        );
        if (account is null)
        {
            return;
        }

        // The email address and its verification status are first-class account fields, not profile claims, so the
        // email scope projects them directly (email_verified as a JSON boolean, which the suite checks by type).
        if (wantsEmail && !string.IsNullOrEmpty(account.Email))
        {
            TryAdd(claims, JoseClaimNames.Payload.Email, account.Email);
            TryAdd(claims, JoseClaimNames.Payload.EmailVerified, account.EmailVerified);
        }

        if (claimTypes.Count == 0)
        {
            return;
        }

        foreach (var claim in account.Claims)
        {
            if (claimTypes.Contains(claim.Type))
            {
                TryAdd(claims, claim.Type, claim.Value);
            }
        }
    }

    private static IReadOnlySet<string> GetGrantedScopes(ClaimsPrincipal subject)
    {
        // RFC 9068 §2.2.3 carries the granted scopes on the access token as a single space-delimited 'scope' claim.
        var scope = subject.FindFirst(JoseClaimNames.Payload.Scope)?.Value;
        if (string.IsNullOrEmpty(scope))
        {
            return FrozenSet<string>.Empty;
        }

        return scope
            .Split(
                OpenIdConstants.ParameterSeparatorChar,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            )
            .ToHashSet(StringComparer.Ordinal);
    }

    private static HashSet<string> CollectScopeClaimTypes(IReadOnlySet<string> scopes)
    {
        var claimTypes = new HashSet<string>(StringComparer.Ordinal);

        if (scopes.Contains(OpenIdConstants.ScopeTypes.Profile))
        {
            claimTypes.UnionWith(OpenIdConstants.ClaimsByScope.Profile);
        }

        if (scopes.Contains(OpenIdConstants.ScopeTypes.Address))
        {
            claimTypes.UnionWith(OpenIdConstants.ClaimsByScope.Address);
        }

        if (scopes.Contains(OpenIdConstants.ScopeTypes.Phone))
        {
            claimTypes.UnionWith(OpenIdConstants.ClaimsByScope.Phone);
        }

        return claimTypes;
    }

    private async ValueTask<PersistedLocalAccount?> ResolveLocalAccountAsync(
        string principalId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);

        // The principal owns a one-to-one self-issued connection whose subject is the local account id (ADR-0051); the
        // caller presenting a non-local (external-IdP) principal simply resolves no local account and is skipped.
        var identityStore = storeManager.GetStore<IFederatedIdentityStore>();
        var identities = await identityStore.GetByPrincipalAsync(principalId, cancellationToken);

        var localAccountId = identities
            .FirstOrDefault(identity =>
                string.Equals(
                    identity.Issuer,
                    AccountConstants.SelfIssuer,
                    StringComparison.Ordinal
                )
            )
            ?.Subject;
        if (string.IsNullOrEmpty(localAccountId))
        {
            return null;
        }

        var accountStore = storeManager.GetStore<ILocalAccountStore>();
        return await accountStore.GetByIdOrDefaultAsync(localAccountId, cancellationToken);
    }

    private static void TryAdd(IDictionary<string, object> claims, string claimType, object value)
    {
        // Do not override a claim another enricher already supplied (for example the required 'sub').
        claims.TryAdd(claimType, value);
    }
}
