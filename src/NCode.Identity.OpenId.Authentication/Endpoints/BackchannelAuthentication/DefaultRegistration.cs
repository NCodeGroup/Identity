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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Handlers;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Logic;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Results;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Messages;
using NCode.Mediator;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication;

/// <summary>
/// Provides extension methods to configure services and handlers for the OpenId backchannel authentication endpoint
/// (OpenID Connect CIBA).
/// </summary>
internal static class DefaultRegistration
{
    extension(IEndpointGroupBuilder builder)
    {
        /// <summary>
        /// Configures services and handlers for the OpenId backchannel authentication endpoint.
        /// </summary>
        public void AddBackchannelAuthenticationEndpoint()
        {
            builder.AddEndpoint<DefaultBackchannelAuthenticationEndpointHandler>();
            builder.AddMessageFactory<BackchannelAuthenticationRequest>();

            var serviceCollection = builder.ServiceCollection;

            serviceCollection.TryAddSingleton<
                ICommandResponseHandler<
                    LoadBackchannelAuthenticationRequestCommand,
                    IBackchannelAuthenticationRequest
                >,
                DefaultLoadBackchannelAuthenticationRequestHandler
            >();

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<ValidateBackchannelAuthenticationRequestCommand>,
                    DefaultValidateBackchannelAuthenticationRequestHandler
                >()
            );

            // The login hint cannot be resolved until a host registers a CIBA authentication capability, which replaces
            // this default handler; without it a backchannel request is rejected with unknown_user_id.
            serviceCollection.TryAddSingleton<
                ICommandResponseHandler<
                    AuthenticateBackchannelRequestCommand,
                    AuthenticateSubjectDisposition
                >,
                DefaultUnsupportedBackchannelAuthenticationHandler
            >();

            serviceCollection.TryAddSingleton<
                ICommandResponseHandler<
                    CreateBackchannelAuthenticationRequestCommand,
                    BackchannelAuthenticationResult
                >,
                DefaultCreateBackchannelAuthenticationRequestHandler
            >();

            // Deployment-specific transport and approval seams with safe defaults a host replaces.
            serviceCollection.TryAddSingleton<
                ICibaNotificationService,
                DefaultCibaNotificationService
            >();
            serviceCollection.TryAddSingleton<ICibaRequestService, DefaultCibaRequestService>();
        }
    }
}
