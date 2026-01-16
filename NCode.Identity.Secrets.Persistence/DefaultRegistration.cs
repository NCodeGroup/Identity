#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.Secrets.Persistence.Encodings;
using NCode.Identity.Secrets.Persistence.Logic;
using NCode.Registration;

namespace NCode.Identity.Secrets.Persistence;

/// <summary>
/// Provides extension methods to configure services and handlers for Identity Secrets Persistence.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder{T}"/> to configure services for <see cref="SecretsLibrary"/>.</param>
    extension(IServiceBuilder<SecretsLibrary> builder)
    {
        /// <summary>
        /// Configures services and handlers for Identity Secrets Persistence.
        /// </summary>
        [PublicAPI]
        public IServiceBuilder<SecretPersistenceLibrary> AddSecretPersistence()
        {
            var newBuilder = builder.NewBuilder<SecretPersistenceLibrary>();

            newBuilder.AddEncoding<BasicSecretEncoding>();

            var serviceCollection = builder.ServiceCollection;
            serviceCollection.TryAddSingleton<ISecretSerializer, DefaultSecretSerializer>();

            return newBuilder;
        }
    }

    /// <param name="builder">The <see cref="IServiceBuilder{T}"/> to configure services.</param>
    extension(IServiceBuilder<SecretPersistenceLibrary> builder)
    {
        /// <summary>
        /// Registers the specified <typeparamref name="T"/> implementation for the <see cref="ISecretEncoding"/> abstraction.
        /// </summary>
        /// <typeparam name="T">The type of the <see cref="ISecretEncoding"/> implementation to register.</typeparam>
        [PublicAPI]
        public IServiceBuilder<SecretPersistenceLibrary> AddEncoding<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>()
            where T : class, ISecretEncoding
        {
            var serviceCollection = builder.ServiceCollection;
            serviceCollection.AddSecretPersistenceEncoding<T>();
            return builder;
        }
    }

    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to configure services.</param>
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Registers the specified <typeparamref name="T"/> implementation for the <see cref="ISecretEncoding"/> abstraction.
        /// </summary>
        /// <typeparam name="T">The type of the <see cref="ISecretEncoding"/> implementation to register.</typeparam>
        [PublicAPI]
        public IServiceCollection AddSecretPersistenceEncoding<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>()
            where T : class, ISecretEncoding
        {
            serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton<ISecretEncoding, T>());
            return serviceCollection;
        }
    }
}
