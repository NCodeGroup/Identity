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
using NCode.Identity.OpenId.Authentication.Tokens.Commands;
using NCode.Identity.OpenId.Authentication.Tokens.Handlers;
using NCode.Mediator;
using NCode.Registration;

namespace NCode.Identity.OpenId.Authentication.Tokens;

/// <summary>
/// Provides extension methods to configure services and handlers for OpenId Token services.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <summary>
    /// Configures services and handlers for OpenId Token services.
    /// </summary>
    /// <param name="builder">The <see cref="IServiceBuilder"/> to configure services for <see cref="OpenIdAuthenticationLibrary"/>.</param>
    /// <returns>The <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
    public static IServiceBuilder<OpenIdAuthenticationLibrary> AddTokenServices(
        this IServiceBuilder<OpenIdAuthenticationLibrary> builder
    )
    {
        var serviceCollection = builder.ServiceCollection;

        serviceCollection.TryAddSingleton<
            ITokenService,
            DefaultTokenService>();

        serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton<
            ICommandHandler<GetIdTokenSubjectClaimsCommand>,
            DefaultGetIdTokenSubjectClaimsHandler>());

        serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton<
            ICommandHandler<GetIdTokenPayloadClaimsCommand>,
            DefaultGetIdTokenPayloadClaimsHandler>());

        serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton<
            ICommandHandler<GetAccessTokenSubjectClaimsCommand>,
            DefaultGetAccessTokenSubjectClaimsHandler>());

        serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton<
            ICommandHandler<GetAccessTokenPayloadClaimsCommand>,
            DefaultGetAccessTokenPayloadClaimsHandler>());

        return builder;
    }
}
