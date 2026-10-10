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
using NCode.Identity.OpenId.Authentication.Endpoints.UserInfo.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.UserInfo.Handlers;
using NCode.Mediator;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Endpoints.UserInfo;

/// <summary>
/// Provides extension methods to configure services and handlers for the OpenId <c>UserInfo</c> endpoint.
/// </summary>
internal static class DefaultRegistration
{
    extension(IEndpointGroupBuilder builder)
    {
        /// <summary>
        /// Configures services and handlers for the OpenId <c>UserInfo</c> endpoint (OpenID Connect Core 5.3).
        /// </summary>
        public void AddUserInfoEndpoint()
        {
            builder.AddEndpoint<DefaultUserInfoEndpointProvider>();

            builder.ServiceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<GetUserInfoClaimsCommand>,
                    DefaultGetUserInfoClaimsHandler
                >()
            );

            builder.ServiceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<GetUserInfoClaimsCommand>,
                    DefaultGetRequestedUserInfoClaimsHandler
                >()
            );

            builder.ServiceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<GetUserInfoClaimsCommand>,
                    DefaultGetUserInfoMetadataClaimsHandler
                >()
            );
        }
    }
}
