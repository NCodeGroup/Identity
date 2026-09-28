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

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.Secrets.Persistence.Logic;
using NCode.Registration;

namespace NCode.Identity.Secrets.Persistence;

/// <summary>
/// Provides extension methods for <see cref="IServiceBuilder{T}"/> to register services
/// required for persisting and serializing identity secrets.
/// </summary>
/// <remarks>
/// <para>
/// This class extends the <see cref="SecretsLibrary"/> service builder to add persistence capabilities,
/// including secret serialization for storage in databases or other persistent stores.
/// </para>
/// </remarks>
[PublicAPI]
public static class DefaultRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder{T}"/> instance for configuring
    /// <see cref="SecretsLibrary"/> services.</param>
    extension(IServiceBuilder<SecretsLibrary> builder)
    {
        /// <summary>
        /// Adds services required for persisting identity secrets, including serialization support.
        /// </summary>
        /// <returns>A new <see cref="IServiceBuilder{T}"/> for <see cref="SecretPersistenceLibrary"/>
        /// that can be used to further configure persistence-related services.</returns>
        /// <remarks>
        /// <para>
        /// This method registers the following services:
        /// </para>
        /// <list type="bullet">
        /// <item><description><see cref="ISecretSerializer"/> - Serializes and deserializes secret keys
        /// for persistent storage.</description></item>
        /// </list>
        /// <para>
        /// Services are registered using <c>TryAddSingleton</c> semantics, meaning existing registrations
        /// will not be overwritten.
        /// </para>
        /// </remarks>
        [PublicAPI]
        public IServiceBuilder<SecretPersistenceLibrary> AddPersistenceLibrary()
        {
            var newBuilder = builder.NewBuilder<SecretPersistenceLibrary>();

            var serviceCollection = builder.ServiceCollection;
            serviceCollection.TryAddSingleton<ISecretSerializer, DefaultSecretSerializer>();

            return newBuilder;
        }
    }
}
