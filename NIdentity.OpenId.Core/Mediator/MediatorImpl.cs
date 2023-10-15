﻿#region Copyright Preamble

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

namespace NIdentity.OpenId.Mediator;

internal class MediatorImpl : IMediator
{
    private IServiceProvider ServiceProvider { get; }
    private ConcurrentDictionary<Type, object> HandlerCache { get; } = new();

    public MediatorImpl(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
    }

    public ValueTask SendAsync(ICommand command, CancellationToken cancellationToken)
    {
        var wrapper = (ICommandHandlerWrapper)HandlerCache.GetOrAdd(command.GetType(), typeOfCommand =>
        {
            var typeOfWrapper = typeof(CommandHandlerWrapper<>).MakeGenericType(typeOfCommand);
            return ActivatorUtilities.CreateInstance(ServiceProvider, typeOfWrapper);
        });
        return wrapper.HandleAsync(command, cancellationToken);
    }

    public ValueTask<TResponse> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken)
    {
        var wrapper = (ICommandResponseHandlerWrapper<TResponse>)HandlerCache.GetOrAdd(command.GetType(), typeOfCommand =>
        {
            var typeOfWrapper = typeof(CommandResponseHandlerWrapper<,>).MakeGenericType(typeOfCommand, typeof(TResponse));
            return ActivatorUtilities.CreateInstance(ServiceProvider, typeOfWrapper);
        });
        return wrapper.HandleAsync(command, cancellationToken);
    }
}
