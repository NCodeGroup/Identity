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

using System.Globalization;
using System.Security.Claims;
using NCode.Buffers;
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Identity.OpenId.Authentication.Interactive;
using NCode.Identity.OpenId.Authentication.Passwords;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Settings;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Accounts.Authentication;

/// <summary>
/// Provides the local-account implementation of the <see cref="IInteractiveCredentialVerifier"/> seam, backed by the
/// configured <see cref="ILocalAccountStore"/> — the same credential path the resource-owner password grant uses. It
/// verifies the submitted username/password and, on success, builds the cookie <see cref="ClaimsPrincipal"/> carrying
/// the subject, issuer, tenant, and authentication-time claims the authorize flow reads. A host fronting a different
/// backend (LDAP, an external identity provider, passwordless) registers its own implementation instead. A password for
/// an account that reached its limit of failed attempts is verified against the store's dummy hash and refused, as a
/// wrong one is.
/// </summary>
internal sealed class LocalAccountInteractiveCredentialVerifier(
    IStoreManagerFactory storeManagerFactory,
    IAuthenticationContextReferenceResolver authenticationContextReferenceResolver,
    TimeProvider timeProvider,
    IPasswordAttemptLimiter passwordAttemptLimiter
) : IInteractiveCredentialVerifier
{
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IAuthenticationContextReferenceResolver AuthenticationContextReferenceResolver { get; } =
        authenticationContextReferenceResolver;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private IPasswordAttemptLimiter PasswordAttemptLimiter { get; } = passwordAttemptLimiter;

    /// <inheritdoc />
    public async ValueTask<ClaimsPrincipal?> VerifyCredentialAsync(
        OpenIdContext openIdContext,
        string username,
        string password,
        CancellationToken cancellationToken
    )
    {
        var result = await VerifyCredentialsAsync(
            openIdContext,
            username,
            password,
            cancellationToken
        );

        // A password refused at the limit is verified against the store's dummy hash and answered as a wrong one, and
        // both count as a failure under the user name the store holds. A disabled account goes on to the sign-in's
        // standing check, which refuses it with its reason. See ADR-0229, ADR-0237 and ADR-0244.
        if (!result.IsVerified)
        {
            // A failure is recorded even when the client disconnects.
            await PasswordAttemptLimiter.RecordFailureAsync(
                openIdContext,
                result.UserName,
                CancellationToken.None
            );
            return null;
        }

        await PasswordAttemptLimiter.ResetAsync(
            openIdContext.Tenant.TenantId,
            result.UserName,
            cancellationToken
        );

        return await BuildCookiePrincipalAsync(openIdContext, result.Account, cancellationToken);
    }

    private async ValueTask<LocalAccountVerificationResult> VerifyCredentialsAsync(
        OpenIdContext openIdContext,
        string username,
        string password,
        CancellationToken cancellationToken
    )
    {
        // Encode the password into a pinned, zeroed-on-return buffer so the credential never lingers on the GC heap.
        var byteCount = SecureEncoding.UTF8.GetByteCount(password);
        using var owner = SecureMemoryPool<byte>.Shared.Rent(byteCount);
        var written = SecureEncoding.UTF8.GetBytes(password, owner.Memory.Span);

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ILocalAccountStore>();

        // The store asks the gate once, with the user name it holds, between its lookup and its verification. The gate
        // refuses a result that a store verified after a refusal and fails a store that never asked, so no store lifts
        // the limit. See ADR-0237 and ADR-0244.
        var gate = new PasswordAttemptGate(PasswordAttemptLimiter, openIdContext);
        var storedResult = await store.VerifyCredentialAsync(
            username,
            owner.Memory[..written],
            gate.IsRefusedAsync,
            cancellationToken
        );
        var result = gate.Enforce(storedResult);

        // Persist a transparent rehash-on-verify, when one occurred, within this unit of work.
        if (result.IsVerified)
        {
            await storeManager.SaveChangesAsync(cancellationToken);
        }

        return result;
    }

    private async ValueTask<ClaimsPrincipal> BuildCookiePrincipalAsync(
        OpenIdContext openIdContext,
        PersistedLocalAccount account,
        CancellationToken cancellationToken
    )
    {
        var settings = openIdContext.Tenant.SettingsProvider.Collection;
        var subjectClaimType = settings.GetValue(OpenIdSettingKeys.SubjectClaimTypes).First();
        var issuerClaimType = settings.GetValue(OpenIdSettingKeys.PrincipalIssuerClaim);

        var authTime = TimeProvider.GetUtcNow().ToUnixTimeSeconds();
        var claims = new List<Claim>(account.Claims.Count + 6)
        {
            new(subjectClaimType, account.LocalAccountId),
            new(issuerClaimType, AccountConstants.SelfIssuer),
            new(JoseClaimNames.Payload.Tid, openIdContext.Tenant.TenantId),
            new(
                JoseClaimNames.Payload.AuthTime,
                authTime.ToString(CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64
            ),
        };
        claims.AddRange(account.Claims.Select(claim => new Claim(claim.Type, claim.Value)));

        // The local account verified a password, so record 'pwd' (RFC 8176) and resolve the authentication context
        // class reference (acr) from it, letting the deployment's policy assert a stronger value when applicable.
        claims.Add(
            new Claim(JoseClaimNames.Payload.Amr, OpenIdProtocol.AuthenticationMethods.Password)
        );

        var acr = await AuthenticationContextReferenceResolver.ResolveAsync(
            new AuthenticationContextReferenceRequest
            {
                AuthenticationMethods = [OpenIdProtocol.AuthenticationMethods.Password],
                IdentityProvider = null,
                Settings = settings,
            },
            cancellationToken
        );
        if (!string.IsNullOrEmpty(acr))
        {
            claims.Add(new Claim(JoseClaimNames.Payload.Acr, acr));
        }

        var identity = new ClaimsIdentity(
            claims,
            InteractiveAuthenticationDefaults.CookieSchemeName
        );
        return new ClaimsPrincipal(identity);
    }
}
