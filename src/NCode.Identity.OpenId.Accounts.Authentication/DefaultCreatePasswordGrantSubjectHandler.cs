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
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Settings;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Accounts.Authentication;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="CreatePasswordGrantSubjectCommand"/> message that
/// builds the subject from a validated local account: the self-issued connection's subject and issuer claims plus the
/// account's profile claims. The local account is a self-issued connection, so principal resolution and linking treat
/// it uniformly with any federated identity. The principal's metadata bags are not carried here;
/// they are a principal-level concept resolved fresh at issuance by the subject-claims projection handlers.
/// </summary>
// See ADR-0035, ADR-0051, ADR-0055.
internal class DefaultCreatePasswordGrantSubjectHandler(
    IAuthenticationContextReferenceResolver authenticationContextReferenceResolver,
    TimeProvider timeProvider
) : ICommandResponseHandler<CreatePasswordGrantSubjectCommand, ClaimsPrincipal>
{
    private IAuthenticationContextReferenceResolver AuthenticationContextReferenceResolver { get; } =
        authenticationContextReferenceResolver;
    private TimeProvider TimeProvider { get; } = timeProvider;

    /// <inheritdoc />
    public async ValueTask<ClaimsPrincipal> HandleAsync(
        CreatePasswordGrantSubjectCommand command,
        CancellationToken cancellationToken
    )
    {
        var (openIdContext, account) = command;

        var settings = openIdContext.Tenant.SettingsProvider.Collection;
        var subjectClaimType = settings.GetValue(OpenIdSettingKeys.SubjectClaimTypes).First();
        var issuerClaimType = settings.GetValue(OpenIdSettingKeys.PrincipalIssuerClaim);

        // The ROPC subject is built fresh with no interactive challenge to stash the tenant and authentication time in
        // auth properties, so both are stamped as claims for DefaultValidateSubjectAuthenticationHandler. The password
        // was just verified, so the authentication time is now.
        var authTime = TimeProvider.GetUtcNow().ToUnixTimeSeconds();
        var claims = new List<Claim>(account.Claims.Count + 6)
        {
            // The subject is stamped under the tenant's primary subject claim type so principal resolution reads it
            // back through the same tenant-aware GetSubjectId seam (ADR-0053).
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

        // The password was just verified, so record 'pwd' (RFC 8176) and resolve the authentication context class
        // reference (acr) from it, letting the deployment's policy assert a stronger value when applicable.
        claims.Add(
            new Claim(JoseClaimNames.Payload.Amr, OpenIdConstants.AuthenticationMethods.Password)
        );

        var acr = await AuthenticationContextReferenceResolver.ResolveAsync(
            new AuthenticationContextReferenceRequest
            {
                AuthenticationMethods = [OpenIdConstants.AuthenticationMethods.Password],
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
            authenticationType: OpenIdProtocol.GrantTypes.Password
        );

        return new ClaimsPrincipal(identity);
    }
}
