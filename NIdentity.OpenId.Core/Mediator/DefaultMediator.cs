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

using Microsoft.Extensions.DependencyInjection;
using NIdentity.OpenId.Mediator.Wrappers;

namespace NIdentity.OpenId.Mediator;

/// <summary>
/// Provides a default implementation for the <see cref="IMediator"/> abstraction.
/// </summary>
public class DefaultMediator : IMediator
{
    private IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultMediator"/> class.
    /// </summary>
    public DefaultMediator(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public ValueTask SendAsync<TCommand>(
        TCommand command,
        CancellationToken cancellationToken
    ) where TCommand : ICommand
    {
        var wrapper = ServiceProvider.GetRequiredService<ICommandHandlerWrapper<TCommand>>();
        return wrapper.HandleAsync(command, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<TResponse> SendAsync<TCommand, TResponse>(
        TCommand command,
        CancellationToken cancellationToken
    ) where TCommand : ICommand<TResponse>
    {
        var wrapper = ServiceProvider.GetRequiredService<ICommandResponseHandlerWrapper<TCommand, TResponse>>();
        return wrapper.HandleAsync(command, cancellationToken);
    }
}
