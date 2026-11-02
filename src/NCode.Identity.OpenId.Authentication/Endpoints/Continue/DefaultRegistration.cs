// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Continue;

/// <summary>
/// Provides extension methods to configure services and handlers for the OpenId Continue endpoint.
/// </summary>
internal static class DefaultRegistration
{
    extension(IEndpointGroupBuilder builder)
    {
        /// <summary>
        /// Configures services and handlers for the OpenId Continue endpoint.
        /// </summary>
        public void AddContinueEndpoint()
        {
            builder.AddEndpoint<DefaultContinueEndpointHandler>();

            var serviceCollection = builder.ServiceCollection;

            serviceCollection.TryAddSingleton<IContinueService, DefaultContinueService>();
            serviceCollection.TryAddSingleton<
                IContinueProviderSelector,
                DefaultContinueProviderSelector
            >();
            serviceCollection.TryAddSingleton<
                IContinueRefusalService,
                DefaultContinueRefusalService
            >();
        }
    }
}
