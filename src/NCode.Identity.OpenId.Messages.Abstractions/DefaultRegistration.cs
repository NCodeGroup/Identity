// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Registration;

namespace NCode.Identity.OpenId.Messages;

/// <summary>
/// Provides extension methods to register OpenID message factories.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder"/> that is used to configure services.</param>
    extension(IServiceBuilder builder)
    {
        /// <summary>
        /// Registers a default message factory for the specified OpenId message type.
        /// </summary>
        /// <typeparam name="TMessage">The type of the <see cref="OpenIdMessage"/> for the factory.</typeparam>
        public void AddMessageFactory<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMessage
        >()
            where TMessage : OpenIdMessage, new()
        {
            var serviceCollection = builder.ServiceCollection;

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IOpenIdMessageFactory,
                    DefaultOpenIdMessageFactory<TMessage>
                >()
            );
        }
    }
}
