#region Copyright Preamble

// Copyright @ 2025 NCode Group
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

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Mediator;
using NCode.Registration;

namespace NCode.Identity.OpenId.Authentication.Subject;

/// <summary>
/// Provides extension methods to configure services and handlers for OpenId Subject services.
/// </summary>
internal static class DefaultRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder"/> to configure services for <see cref="OpenIdAuthenticationLibrary"/>.</param>
    extension(IServiceBuilder<OpenIdAuthenticationLibrary> builder)
    {
        /// <summary>
        /// Configures services and handlers for OpenId Subject services.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        [PublicAPI]
        public IServiceBuilder<OpenIdAuthenticationLibrary> AddSubjectServices()
        {
            var serviceCollection = builder.ServiceCollection;

            serviceCollection.TryAddSingleton<
                ISubjectIdentifierService,
                DefaultSubjectIdentifierService
            >();

            serviceCollection.TryAddSingleton<
                IAuthenticationContextReferenceResolver,
                DefaultAuthenticationContextReferenceResolver
            >();

            serviceCollection.TryAddSingleton<
                ICommandHandler<ValidateSubjectAuthenticationCommand>,
                DefaultValidateSubjectAuthenticationHandler
            >();

            // Every validation of a subject includes its standing; it is added after the default handler, which
            // TryAddSingleton registers only when no handler exists.
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<ValidateSubjectAuthenticationCommand>,
                    DefaultValidateStandingOnSubjectAuthenticationHandler
                >()
            );

            // A blocked principal is refused wherever a subject's standing is validated.
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<ValidateSubjectStandingCommand>,
                    DefaultValidateBlockedSubjectHandler
                >()
            );

            serviceCollection.TryAddSingleton<
                ICommandResponseHandler<AuthenticateSubjectCommand, AuthenticateSubjectDisposition>,
                DefaultAuthenticateSubjectHandler
            >();

            serviceCollection.TryAddSingleton<
                ICommandResponseHandler<
                    AuthenticateResourceRequestCommand,
                    AuthenticateSubjectDisposition
                >,
                DefaultAuthenticateSubjectHandler
            >();

            return builder;
        }
    }
}
