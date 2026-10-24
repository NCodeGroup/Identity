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
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.OpenId.Authentication;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.UserInfo.Commands;
using NCode.Identity.OpenId.Authentication.Interactive;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Mediator;
using NCode.Registration;

namespace NCode.Identity.OpenId.Accounts.Authentication;

/// <summary>
/// Provides extension methods to register the local-account authentication capability: the resource-owner password
/// grant handlers that consume the configured <c>ILocalAccountStore</c>.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <param name="builder">The <see cref="OpenIdAuthenticationLibrary"/> builder these capabilities extend.</param>
    extension(IServiceBuilder<OpenIdAuthenticationLibrary> builder)
    {
        /// <summary>
        /// Registers the resource-owner password grant capability over the configured <c>ILocalAccountStore</c>. This
        /// overrides the base <c>NCode.Identity.OpenId.Authentication</c> handler that reports the password grant as
        /// unsupported, so calling it is what enables the grant. It also registers the account-sourced UserInfo
        /// scope-claims enricher, so the UserInfo endpoint returns the subject's profile/email/address/phone claims
        /// from the local account (OpenID Connect Core 1.0 §5.4). It also registers the subject-validation handler that
        /// refuses a subject whose local account is disabled or no longer exists.
        /// </summary>
        /// <param name="localAccountStore">The local-account store builder — the compile-time proof that a
        /// <c>ILocalAccountStore</c> is registered, so this capability cannot be added without one.</param>
        /// <returns>The same <see cref="IServiceBuilder{T}"/> for method chaining.</returns>
        public IServiceBuilder<OpenIdAuthenticationLibrary> AddLocalAccountAuthentication(
            IServiceBuilder<LocalAccountLibrary> localAccountStore
        )
        {
            ArgumentNullException.ThrowIfNull(localAccountStore);

            var serviceCollection = builder.ServiceCollection;

            // Replace (not TryAdd) so this capability wins over the base "unsupported password grant" default handler.
            serviceCollection.Replace(
                ServiceDescriptor.Singleton<
                    ICommandResponseHandler<
                        AuthenticatePasswordGrantCommand,
                        AuthenticateSubjectDisposition
                    >,
                    DefaultAuthenticatePasswordGrantHandler
                >()
            );

            serviceCollection.TryAddSingleton<
                ICommandResponseHandler<CreatePasswordGrantSubjectCommand, ClaimsPrincipal>,
                DefaultCreatePasswordGrantSubjectHandler
            >();

            AddLocalAccountUserInfoClaims(serviceCollection);
            AddLocalAccountSubjectValidation(serviceCollection);

            return builder;
        }

        /// <summary>
        /// Registers the local-account implementation of the <see cref="IInteractiveCredentialVerifier"/> seam, so the
        /// opt-in interactive login/consent surface (<c>AddInteractiveAuthentication</c>) verifies credentials against
        /// the configured <c>ILocalAccountStore</c> — the same credential path the resource-owner password grant uses.
        /// The host registers the interactive surface separately. A host fronting a different backend (LDAP, an external
        /// identity provider, passwordless) registers its own <see cref="IInteractiveCredentialVerifier"/> instead. It
        /// also registers the subject-validation handler that refuses a subject whose local account is disabled or no
        /// longer exists, and the <see cref="IInteractivePasswordChanger"/> that lets an end user change the password
        /// of their local account on the self-service surface.
        /// </summary>
        /// <param name="localAccountStore">The local-account store builder — the compile-time proof that a
        /// <c>ILocalAccountStore</c> is registered, so this capability cannot be added without one.</param>
        /// <returns>The same <see cref="IServiceBuilder{T}"/> for method chaining.</returns>
        public IServiceBuilder<OpenIdAuthenticationLibrary> AddLocalAccountInteractiveAuthentication(
            IServiceBuilder<LocalAccountLibrary> localAccountStore
        )
        {
            ArgumentNullException.ThrowIfNull(localAccountStore);

            var serviceCollection = builder.ServiceCollection;

            serviceCollection.TryAddSingleton<
                IInteractiveCredentialVerifier,
                LocalAccountInteractiveCredentialVerifier
            >();

            // Replace (not TryAdd) so this capability wins over the surface's default changer, which reports that no
            // change is supported, whatever the order the two registrations run in.
            // See ADR-0230.
            serviceCollection.Replace(
                ServiceDescriptor.Singleton<
                    IInteractivePasswordChanger,
                    LocalAccountInteractivePasswordChanger
                >()
            );

            AddLocalAccountUserInfoClaims(serviceCollection);
            AddLocalAccountSubjectValidation(serviceCollection);

            return builder;
        }
    }

    // The account-sourced UserInfo enricher is grant-agnostic (any access token issued for a local account), so both
    // the password-grant and interactive registrations wire it; TryAddEnumerable keys on the implementation type, so
    // registering it from both entry points adds it exactly once.
    private static void AddLocalAccountUserInfoClaims(IServiceCollection serviceCollection) =>
        serviceCollection.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                ICommandHandler<GetUserInfoClaimsCommand>,
                DefaultGetLocalAccountUserInfoClaimsHandler
            >()
        );

    // The subject-validation handler is grant-agnostic (it runs wherever an end user's standing is validated), so both
    // the password-grant and interactive registrations wire it; TryAddEnumerable adds it exactly once.
    private static void AddLocalAccountSubjectValidation(IServiceCollection serviceCollection) =>
        serviceCollection.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                ICommandHandler<ValidateSubjectStandingCommand>,
                DefaultValidateLocalAccountSubjectHandler
            >()
        );
}
