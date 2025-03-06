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
using NCode.Collections.Providers;
using NCode.Identity.OpenId.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Endpoints.Authorization.Results;
using NCode.Identity.OpenId.Endpoints.Token.Messages;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Messages.Commands;
using NCode.Identity.OpenId.Messages.Handlers;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Messages;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> to register required services and handlers for OpenId messages
/// </summary>
[PublicAPI]
public static class DefaultOpenIdMessageRegistration
{
    /// <summary>
    /// Registers the required services and handlers for OpenId messages into the provided <see cref="IServiceCollection"/> instance.
    /// </summary>
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <returns>The <see cref="IServiceCollection"/> instance for method chaining.</returns>
    public static IServiceCollection AddOpenIdMessages(
        this IServiceCollection serviceCollection
    )
    {
        serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton<
            ICollectionDataSource<KnownParameter>,
            DefaultKnownParameterDataSource>());

        serviceCollection.TryAddSingleton<
            IKnownParameterCollectionProvider,
            DefaultKnownParameterCollectionProvider>();

        serviceCollection.TryAddSingleton<
            IOpenIdMessageFactorySelector,
            DefaultOpenIdMessageFactorySelector>();

        serviceCollection.TryAddSingleton<
            ICommandResponseHandler<LoadRequestValuesCommand, IRequestValues>,
            DefaultLoadRequestValuesHandler>();

        serviceCollection.AddMessageFactory<OpenIdError>();
        serviceCollection.AddMessageFactory<OpenIdMessage>();

        serviceCollection.AddMessageFactory<AuthorizationRequestMessage>();
        serviceCollection.AddMessageFactory<AuthorizationRequestObject>();
        serviceCollection.AddMessageFactory<AuthorizationTicket>();

        serviceCollection.AddMessageFactory<TokenRequest>();
        serviceCollection.AddMessageFactory<TokenResponse>();

        return serviceCollection;
    }

    /// <summary>
    /// Registers a default message factory for the specified message type into the provided <see cref="IServiceCollection"/> instance.
    /// </summary>
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <typeparam name="TMessage">The type of the <see cref="OpenIdMessage"/> for the factory.</typeparam>
    /// <returns>The <see cref="IServiceCollection"/> instance for method chaining.</returns>
    public static IServiceCollection AddMessageFactory<TMessage>(
        this IServiceCollection serviceCollection
    )
        where TMessage : OpenIdMessage, new()
    {
        serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton<
            IOpenIdMessageFactory,
            DefaultOpenIdMessageFactory<TMessage>>());

        return serviceCollection;
    }
}
