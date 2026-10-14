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
