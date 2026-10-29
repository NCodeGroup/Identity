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

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.Jose;
using NCode.Identity.Jwt.Claims;
using NCode.Identity.Jwt.Encoding;
using NCode.Identity.Jwt.Validation;
using NCode.Registration;

namespace NCode.Identity.Jwt;

/// <summary>
/// Provides extension methods for <see cref="IServiceBuilder{T}"/> to register the services needed for encoding and
/// validating JSON Web Tokens (JWT).
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder{T}"/> to configure services for <see cref="JoseLibrary"/>.</param>
    extension(IServiceBuilder<JoseLibrary> builder)
    {
        /// <summary>
        /// Registers the JSON Web Token (JWT) encoder, validator, and built-in validation handlers, layered on the
        /// <see cref="JoseLibrary"/>.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> so that additional calls can be chained.</returns>
        public IServiceBuilder<JwtLibrary> AddJwtLibrary()
        {
            var serviceCollection = builder.ServiceCollection;

            var newBuilder = builder.NewBuilder<JwtLibrary>();

            serviceCollection.TryAddSingleton<IJwtEncoder, DefaultJwtEncoder>();
            serviceCollection.TryAddSingleton<
                IClaimsIdentityFactory,
                DefaultClaimsIdentityFactory
            >();
            serviceCollection.TryAddSingleton<IJwtValidator, DefaultJwtValidator>();

            serviceCollection.TryAddEnumerable([
                ServiceDescriptor.Singleton<IJwtValidationHandler, ClaimValueRequirementHandler>(),
                ServiceDescriptor.Singleton<IJwtValidationHandler, TokenTypeRequirementHandler>(),
                ServiceDescriptor.Singleton<
                    IJwtValidationHandler,
                    TokenLifetimeRequirementHandler
                >(),
                ServiceDescriptor.Singleton<
                    IJwtValidationHandler,
                    CertificateLifetimeRequirementHandler
                >(),
                ServiceDescriptor.Singleton<IJwtValidationHandler, AssertionRequirementHandler>(),
            ]);

            return newBuilder;
        }
    }
}

/// <summary>
/// Provides extension methods for <see cref="IServiceBuilder{T}"/> to register custom JSON Web Token (JWT)
/// validation handlers.
/// </summary>
[PublicAPI]
public static class JwtValidationHandlerRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder{T}"/> to configure services for <see cref="JwtLibrary"/>.</param>
    extension(IServiceBuilder<JwtLibrary> builder)
    {
        /// <summary>
        /// Registers a custom <see cref="IJwtValidationHandler"/> so external code can contribute its own JSON Web
        /// Token (JWT) validation requirements.
        /// </summary>
        /// <typeparam name="THandler">The <see cref="IJwtValidationHandler"/> implementation to register.</typeparam>
        /// <returns>The <see cref="IServiceBuilder{T}"/> so that additional calls can be chained.</returns>
        public IServiceBuilder<JwtLibrary> AddJwtValidationHandler<THandler>()
            where THandler : class, IJwtValidationHandler
        {
            builder.ServiceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IJwtValidationHandler, THandler>()
            );
            return builder;
        }
    }
}
