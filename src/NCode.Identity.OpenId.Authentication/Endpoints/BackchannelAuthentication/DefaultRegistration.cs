// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.Dispatch;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Delivery;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Handlers;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Results;
using NCode.Identity.OpenId.Authentication.OutboundHttp;
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

            // Client ping/push delivery runs as a durable, retryable dispatch job; the handler performs the HTTP POST.
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Scoped<
                    IDispatchJobHandler<CibaDeliveryJob>,
                    CibaDeliveryJobHandler
                >()
            );

            // CIBA ping/push delivery POSTs to the client-registered client_notification_endpoint, so it uses a
            // dedicated SSRF-guarded HTTP client whose connections are pinned away from private/loopback/link-local
            // addresses and which never follows redirects, so a registered internal URL cannot turn the OP into a
            // confused deputy.
            serviceCollection
                .AddHttpClient(CibaDeliveryJobHandler.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(
                    serviceProvider => new System.Net.Http.SocketsHttpHandler
                    {
                        AllowAutoRedirect = false,
                        ConnectCallback = serviceProvider
                            .GetRequiredService<IOutboundHttpForgeryGuard>()
                            .ConnectAsync,
                        AutomaticDecompression = System.Net.DecompressionMethods.All,
                    }
                );
        }
    }
}
