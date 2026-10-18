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

using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Handlers;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Models;
using NCode.Identity.OpenId.Messages;
using NCode.Mediator;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Endpoints.EndSession;

/// <summary>
/// Provides extension methods to configure services and handlers for the OpenId RP-Initiated Logout
/// <c>end_session_endpoint</c>.
/// </summary>
internal static class DefaultRegistration
{
    extension(IEndpointGroupBuilder builder)
    {
        /// <summary>
        /// Configures services and handlers for the OpenId RP-Initiated Logout <c>end_session_endpoint</c>
        /// (RP-Initiated Logout 1.0).
        /// </summary>
        public void AddEndSessionEndpoint()
        {
            builder.AddEndpoint<DefaultEndSessionEndpointProvider>();

            builder.AddMessageFactory<EndSessionRequest>();

            var serviceCollection = builder.ServiceCollection;

            serviceCollection.TryAddSingleton<
                ICommandResponseHandler<LoadEndSessionRequestCommand, IEndSessionRequest>,
                DefaultLoadEndSessionRequestHandler
            >();

            serviceCollection.TryAddSingleton<
                ICommandResponseHandler<ResolveEndSessionSubjectCommand, EndSessionSubject>,
                DefaultResolveEndSessionSubjectHandler
            >();

            serviceCollection.TryAddSingleton<
                ICommandResponseHandler<EndSessionCommand, EndSessionDisposition>,
                DefaultEndSessionHandler
            >();
        }
    }
}
