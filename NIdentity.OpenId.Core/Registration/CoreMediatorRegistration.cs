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

using Microsoft.Extensions.DependencyInjection;
using NIdentity.OpenId.Mediator;
using NIdentity.OpenId.Mediator.Middleware;
using NIdentity.OpenId.Mediator.Wrappers;

namespace NIdentity.OpenId.Registration;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> to register core mediator services and handlers
/// </summary>
public static class CoreMediatorRegistration
{
    /// <summary>
    /// Registers core mediator services and handlers into the provided <see cref="IServiceCollection"/> instance.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> instance.</param>
    /// <returns>The <see cref="IServiceCollection"/> instance for method chaining.</returns>
    public static IServiceCollection AddCoreMediatorServices(this IServiceCollection services)
    {
        services.AddScoped(typeof(ICommandHandlerWrapper<>), typeof(CommandHandlerWrapper<>));
        services.AddScoped(typeof(ICommandResponseHandlerWrapper<,>), typeof(CommandResponseHandlerWrapper<,>));

        services.AddScoped(typeof(CommandExceptionListenerWrapper<,>));
        services.AddScoped(typeof(CommandExceptionHandlerWrapper<,>));
        services.AddScoped(typeof(CommandResponseExceptionHandlerWrapper<,,>));

        services.AddScoped(typeof(ICommandMiddleware<>), typeof(StandardCommandMiddleware<>));
        services.AddScoped(typeof(ICommandResponseMiddleware<,>), typeof(StandardCommandResponseMiddleware<,>));

        services.AddScoped<IMediator, DefaultMediator>();

        return services;
    }
}
