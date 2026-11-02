// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using NCode.Registration;

namespace NCode.Identity.Dispatch;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> to register the background job-dispatch library.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to add services to.</param>
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Mints the <see cref="DispatchLibrary"/> marker that the dispatch opt-ins require. It registers no dispatcher
        /// adapter by itself; call <c>AddInProcessJobDispatcher</c> for the built-in in-process adapter, or register a
        /// custom <see cref="IJobDispatcher"/> for a durable or distributed backend.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> for the dispatch library so that calls can be chained.</returns>
        [PublicAPI]
        public IServiceBuilder<DispatchLibrary> AddDispatchServices()
        {
            ArgumentNullException.ThrowIfNull(serviceCollection);

            return serviceCollection.NewBuilder<DispatchLibrary>();
        }
    }
}
