// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.OpenId.Authentication.Endpoints.DeviceAuthorization.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.DeviceAuthorization.Handlers;
using NCode.Identity.OpenId.Authentication.Endpoints.DeviceAuthorization.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.DeviceAuthorization.Results;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.Registration.AspNetCore;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.DeviceAuthorization;

/// <summary>
/// Provides extension methods to configure services and handlers for the OpenId device authorization endpoints
/// (RFC 8628).
/// </summary>
internal static class DefaultRegistration
{
    extension(IEndpointGroupBuilder builder)
    {
        /// <summary>
        /// Configures services and handlers for the OpenId device authorization and verification endpoints.
        /// </summary>
        public void AddDeviceAuthorizationEndpoint()
        {
            builder.AddEndpoint<DefaultDeviceAuthorizationEndpointHandler>();
            builder.AddEndpoint<DefaultDeviceVerificationEndpointHandler>();
            builder.AddMessageFactory<DeviceAuthorizationRequest>();

            var serviceCollection = builder.ServiceCollection;

            serviceCollection.TryAddSingleton<IUserCodeGenerator, DefaultUserCodeGenerator>();

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<ValidateDeviceAuthorizationRequestCommand>,
                    DefaultValidateDeviceAuthorizationRequestHandler
                >()
            );

            serviceCollection.TryAddSingleton<
                ICommandResponseHandler<
                    CreateDeviceAuthorizationRequestCommand,
                    DeviceAuthorizationResult
                >,
                DefaultCreateDeviceAuthorizationRequestHandler
            >();
        }
    }
}
