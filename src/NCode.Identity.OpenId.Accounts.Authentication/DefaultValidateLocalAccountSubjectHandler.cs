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

using System.Security.Claims;
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Mediator;
using NCode.Persistence.Stores;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Accounts.Authentication;

/// <summary>
/// Provides a <see cref="ValidateSubjectStandingCommand"/> handler that refuses an end user whose local account is
/// disabled or no longer exists, so no session, authorization code or refresh token of such an account issues a token.
/// A disabled local account refuses its principal whatever connection it signed in through. A subject the local
/// accounts cannot judge, because it has no local account and was not authenticated by one, is left to the other
/// handlers.
/// </summary>
// See ADR-0224 and ADR-0229.
internal class DefaultValidateLocalAccountSubjectHandler(IStoreManagerFactory storeManagerFactory)
    : ICommandHandler<ValidateSubjectStandingCommand>
{
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        ValidateSubjectStandingCommand command,
        CancellationToken cancellationToken
    )
    {
        var (openIdContext, subjectAuthentication, operationDisposition) = command;

        // short-circuit if we already have an error
        if (operationDisposition.HasError)
        {
            return;
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);

        var identities = await storeManager
            .GetStore<IFederatedIdentityStore>()
            .GetByPrincipalAsync(subjectAuthentication.PrincipalId, cancellationToken);

        if (identities.Count == 0)
        {
            if (AuthenticatedLocally(openIdContext, subjectAuthentication))
            {
                Refuse(
                    openIdContext,
                    operationDisposition,
                    "The end-user's account no longer exists."
                );
            }

            return;
        }

        var accountStore = storeManager.GetStore<ILocalAccountStore>();
        foreach (var identity in identities)
        {
            if (
                !string.Equals(
                    identity.Issuer,
                    AccountConstants.SelfIssuer,
                    StringComparison.Ordinal
                )
            )
            {
                continue;
            }

            var account = await accountStore.GetByIdOrDefaultAsync(
                identity.Subject,
                cancellationToken
            );
            if (account is not { IsEnabled: true })
            {
                Refuse(
                    openIdContext,
                    operationDisposition,
                    "The end-user's account is disabled or no longer exists."
                );
                return;
            }
        }
    }

    // A principal without an identity is a local account's once its account and identity were deleted; the subject's
    // issuer claim says whether the local accounts signed it in.
    private static bool AuthenticatedLocally(
        OpenIdContext openIdContext,
        SubjectAuthentication subjectAuthentication
    )
    {
        var settings = openIdContext.Tenant.SettingsProvider.Collection;
        var issuerClaimType = settings.GetValue(OpenIdSettingKeys.PrincipalIssuerClaim);
        var issuer = subjectAuthentication.Subject.FindFirstValue(issuerClaimType);
        return string.Equals(issuer, AccountConstants.SelfIssuer, StringComparison.Ordinal);
    }

    private static void Refuse(
        OpenIdContext openIdContext,
        OperationDisposition<IOpenIdError> operationDisposition,
        string message
    )
    {
        operationDisposition.Error ??= openIdContext.ErrorFactory.AccessDenied(message);
    }
}
