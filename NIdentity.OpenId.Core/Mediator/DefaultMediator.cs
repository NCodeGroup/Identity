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

using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using NIdentity.OpenId.Mediator.Wrappers;

namespace NIdentity.OpenId.Mediator;

internal class DefaultMediator : IMediator
{
    private IServiceProvider ServiceProvider { get; }
    private ConcurrentDictionary<Type, object> HandlerCache { get; } = new();

    public DefaultMediator(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
    }

    public ValueTask SendAsync<TCommand>(
        TCommand command,
        CancellationToken cancellationToken
    ) where TCommand : ICommand
    {
        var wrapper = (ICommandHandlerWrapper<TCommand>)HandlerCache.GetOrAdd(typeof(TCommand), commandType =>
        {
            var wrapperType = typeof(CommandHandlerWrapper<>).MakeGenericType(commandType);
            return ActivatorUtilities.CreateInstance(ServiceProvider, wrapperType);
        });
        return wrapper.HandleAsync(command, cancellationToken);
    }

    public ValueTask<TResponse> SendAsync<TCommand, TResponse>(
        TCommand command,
        CancellationToken cancellationToken
    ) where TCommand : ICommand<TResponse>
    {
        var wrapper = (ICommandResponseHandlerWrapper<TCommand, TResponse>)HandlerCache.GetOrAdd(typeof(TCommand), commandType =>
        {
            var wrapperType = typeof(CommandResponseHandlerWrapper<,>).MakeGenericType(commandType, typeof(TResponse));
            return ActivatorUtilities.CreateInstance(ServiceProvider, wrapperType);
        });
        return wrapper.HandleAsync(command, cancellationToken);
    }
}
