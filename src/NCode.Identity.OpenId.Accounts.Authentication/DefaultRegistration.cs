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
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Accounts.Authentication;

/// <summary>
/// Provides extension methods to register the local-account authentication capability: the resource-owner password
/// grant handlers that consume the configured <c>ILocalAccountStore</c>.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Registers the resource-owner password grant capability over the configured <c>ILocalAccountStore</c>. This
        /// overrides the base <c>NCode.Identity.OpenId.Authentication</c> handler that reports the password grant as
        /// unsupported, so calling it is what enables the grant. The host must separately register a local-account
        /// store (for example, the generic Entity Framework reference or the ASP.NET Core Identity adapter).
        /// </summary>
        /// <returns>The <see cref="IServiceCollection"/> instance for method chaining.</returns>
        public IServiceCollection AddLocalAccountAuthentication()
        {
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

            return serviceCollection;
        }
    }
}
