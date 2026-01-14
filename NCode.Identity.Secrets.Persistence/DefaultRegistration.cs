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
    /// <summary>
    /// Configures services and handlers for Identity Secrets Persistence.
    /// </summary>
    /// <param name="builder">The <see cref="IServiceBuilder{T}"/> to configure services for <see cref="SecretsLibrary"/>.</param>
    /// <param name="configure">The action to configure services for <see cref="SecretPersistenceLibrary"/>.</param>
    public static IServiceBuilder<SecretsLibrary> AddPersistenceServices(
        this IServiceBuilder<SecretsLibrary> builder,
        Action<IServiceBuilder<SecretPersistenceLibrary>> configure
    )
    {
        var newBuilder = builder.Register<SecretPersistenceLibrary>();
        configure(newBuilder);

        newBuilder.AddEncoding<BasicSecretEncoding>();

        var serviceCollection = builder.ServiceCollection;
        serviceCollection.TryAddSingleton<ISecretSerializer, DefaultSecretSerializer>();

        return builder;
    }

    /// <summary>
    /// Registers the specified <typeparamref name="T"/> implementation for the <see cref="ISecretEncoding"/> abstraction.
    /// </summary>
    /// <param name="builder">The <see cref="IServiceBuilder{T}"/> to configure services.</param>
    /// <typeparam name="T">The type of the <see cref="ISecretEncoding"/> implementation to register.</typeparam>
    public static IServiceBuilder<SecretPersistenceLibrary> AddEncoding<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        T
    >(
        this IServiceBuilder<SecretPersistenceLibrary> builder
    )
        where T : class, ISecretEncoding
    {
        var serviceCollection = builder.ServiceCollection;
        serviceCollection.AddSecretPersistenceEncoding<T>();
        return builder;
    }

    /// <summary>
    /// Registers the specified <typeparamref name="T"/> implementation for the <see cref="ISecretEncoding"/> abstraction.
    /// </summary>
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to configure services.</param>
    /// <typeparam name="T">The type of the <see cref="ISecretEncoding"/> implementation to register.</typeparam>
    public static IServiceCollection AddSecretPersistenceEncoding<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        T
    >(
        this IServiceCollection serviceCollection
    )
        where T : class, ISecretEncoding
    {
        serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton<ISecretEncoding, T>());
        return serviceCollection;
    }
}
