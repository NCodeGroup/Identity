// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Registration;

namespace NCode.Identity.SharedState;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> to register the shared-state library.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to add services to.</param>
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Mints the <see cref="SharedStateLibrary"/> marker that the shared-state adapters require. It registers no
        /// adapter by itself; call <c>AddInProcessExpiringKeySet</c> for the built-in single-replica adapter, or
        /// register a shared adapter (for example <c>AddValkey</c>) for a deployment that runs several replicas.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> for the shared-state library so that calls can be chained.</returns>
        [PublicAPI]
        public IServiceBuilder<SharedStateLibrary> AddSharedStateServices()
        {
            ArgumentNullException.ThrowIfNull(serviceCollection);

            return serviceCollection.NewBuilder<SharedStateLibrary>();
        }
    }
}
