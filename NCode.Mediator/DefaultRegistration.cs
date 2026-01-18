#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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
using NCode.Mediator.Middleware;
using NCode.Mediator.Wrappers;
using NCode.Registration;

namespace NCode.Mediator;

/// <summary>
/// Provides extension methods to configure services and handlers for Mediator.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to add services to.</param>
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Configures services and handlers for Mediator.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder"/> instance for method chaining.</returns>
        [PublicAPI]
        public IServiceBuilder<MediatorLibrary> AddMediatorLibrary()
        {
            serviceCollection.TryAddScoped(
                typeof(ICommandHandlerWrapper<>),
                typeof(CommandHandlerWrapper<>)
            );

            serviceCollection.TryAddScoped(
                typeof(ICommandResponseHandlerWrapper<,>),
                typeof(CommandResponseHandlerWrapper<,>)
            );

            serviceCollection.TryAddScoped(
                typeof(CommandExceptionListenerWrapper<,>)
            );

            serviceCollection.TryAddScoped(
                typeof(CommandExceptionHandlerWrapper<,>)
            );

            serviceCollection.TryAddScoped(
                typeof(CommandResponseExceptionHandlerWrapper<,,>)
            );

            serviceCollection.TryAddScoped(
                typeof(ICommandMiddleware<>),
                typeof(StandardCommandMiddleware<>)
            );

            serviceCollection.TryAddScoped(
                typeof(ICommandResponseMiddleware<,>),
                typeof(StandardCommandResponseMiddleware<,>)
            );

            serviceCollection.TryAddScoped<
                IMediator,
                DefaultMediator
            >();

            return serviceCollection.NewBuilder<MediatorLibrary>();
        }
    }
}
