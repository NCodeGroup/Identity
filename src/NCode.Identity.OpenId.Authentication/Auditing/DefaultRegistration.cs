// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NCode.Identity.Events;
using NCode.Identity.Events.Audit;
using NCode.Identity.Registration;

namespace NCode.Identity.OpenId.Authentication.Auditing;

/// <summary>
/// Provides extension methods to configure the audit services.
/// </summary>
internal static class DefaultRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder"/> to configure services for <see cref="OpenIdAuthenticationLibrary"/>.</param>
    extension(IServiceBuilder<OpenIdAuthenticationLibrary> builder)
    {
        /// <summary>
        /// Configures the audit event recorder and the audit store's writer: the handler that queues every audit event's
        /// record and the service that writes the queued records to the store, and the service that removes the ones past
        /// their retention.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        [PublicAPI]
        public IServiceBuilder<OpenIdAuthenticationLibrary> AddAuditingServices()
        {
            var serviceCollection = builder.ServiceCollection;

            serviceCollection.TryAddSingleton<IAuditEventRecorder, DefaultAuditEventRecorder>();

            // See ADR-1303.
            serviceCollection.AddOptions<AuditRecordOptions>().ValidateOnStart();
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IValidateOptions<AuditRecordOptions>,
                    AuditRecordOptionsValidator
                >()
            );
            serviceCollection.TryAddSingleton(TimeProvider.System);
            serviceCollection.TryAddSingleton<AuditRecordQueue>();
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IEventHandler<IAuditEvent>, AuditRecordHandler>()
            );
            serviceCollection.AddHostedService<AuditRecordFlushService>();
            serviceCollection.AddHostedService<AuditRecordPruneService>();

            return builder;
        }
    }
}
