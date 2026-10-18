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

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.Dispatch.InProcess;
using NCode.Identity.Dispatch.Options;
using NCode.Registration;

namespace NCode.Identity.Dispatch;

/// <summary>
/// Provides opt-in extension methods for the <see cref="DispatchLibrary"/> builder, so dispatcher and handler
/// registrations cannot be added without first calling <c>AddDispatchServices</c>.
/// </summary>
[PublicAPI]
public static class DispatchLibraryRegistration
{
    /// <param name="builder">The dispatch-library builder returned by <c>AddDispatchServices</c>.</param>
    extension(IServiceBuilder<DispatchLibrary> builder)
    {
        /// <summary>
        /// Registers the built-in in-process, self-scheduling <see cref="IJobDispatcher"/> adapter: an in-memory queue
        /// drained by a hosted worker that retries failed jobs with exponential back-off. The registration is a default
        /// (<c>TryAdd</c>), so a deployment that registered its own <see cref="IJobDispatcher"/> first keeps that
        /// adapter and only adds the hosted worker — omit this call entirely for a fully custom backend.
        /// </summary>
        /// <param name="configureOptions">
        /// An optional callback to configure the in-process queue's <see cref="InProcessJobDispatcherOptions"/>
        /// (capacity and full-queue behavior).
        /// </param>
        /// <returns>The same <see cref="IServiceBuilder{T}"/> so that calls can be chained.</returns>
        [PublicAPI]
        public IServiceBuilder<DispatchLibrary> AddInProcessJobDispatcher(
            Action<InProcessJobDispatcherOptions>? configureOptions = null
        )
        {
            var serviceCollection = builder.ServiceCollection;

            var optionsBuilder = serviceCollection.AddOptions<InProcessJobDispatcherOptions>();
            if (configureOptions is not null)
                optionsBuilder.Configure(configureOptions);

            serviceCollection.TryAddSingleton<InProcessJobQueue>();
            serviceCollection.TryAddSingleton<IJobDispatcher, DefaultInProcessJobDispatcher>();
            serviceCollection.AddHostedService<JobDispatchWorker>();

            return builder;
        }

        /// <summary>
        /// Registers an <see cref="IDispatchJobHandler{TJob}"/> that executes dispatched jobs of type
        /// <typeparamref name="TJob"/>.
        /// </summary>
        /// <typeparam name="TJob">The type of job the handler executes.</typeparam>
        /// <typeparam name="THandler">The concrete handler implementation to register.</typeparam>
        /// <returns>The same <see cref="IServiceBuilder{T}"/> so that calls can be chained.</returns>
        [PublicAPI]
        public IServiceBuilder<DispatchLibrary> AddDispatchJobHandler<TJob, THandler>()
            where TJob : class, IDispatchJob
            where THandler : class, IDispatchJobHandler<TJob>
        {
            builder.ServiceCollection.TryAddEnumerable(
                ServiceDescriptor.Scoped<IDispatchJobHandler<TJob>, THandler>()
            );

            return builder;
        }
    }
}
