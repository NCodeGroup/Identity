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
using NCode.Identity.OpenId.Mediator.Wrappers;

namespace NCode.Identity.OpenId.Mediator;

/// <summary>
/// Provides a default implementation of the <see cref="IMediator"/> abstraction.
/// </summary>
public class DefaultMediator(
    IServiceProvider serviceProvider
) : IMediator
{
    private IServiceProvider ServiceProvider { get; } = serviceProvider;

    /// <inheritdoc />
    public ValueTask SendAsync<TCommand>(
        TCommand command,
        CancellationToken cancellationToken
    ) where TCommand : struct, ICommand
    {
        var wrapper = ServiceProvider.GetRequiredService<ICommandHandlerWrapper<TCommand>>();
        return wrapper.HandleAsync(command, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<TResponse> SendAsync<TCommand, TResponse>(
        TCommand command,
        CancellationToken cancellationToken
    ) where TCommand : struct, ICommand<TResponse>
    {
        var wrapper = ServiceProvider.GetRequiredService<ICommandResponseHandlerWrapper<TCommand, TResponse>>();
        return wrapper.HandleAsync(command, cancellationToken);
    }

    //

    /// <inheritdoc />
    public ValueTask PolymorphicSendAsync(
        ICommand command,
        CancellationToken cancellationToken
    )
    {
        var wrapperType = typeof(ICommandHandlerWrapper<>).MakeGenericType(command.GetType());
        var wrapper = (ICommandHandlerWrapper)ServiceProvider.GetRequiredService(wrapperType);
        return wrapper.HandleAsync(command, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<TResponse> PolymorphicSendAsync<TResponse>(
        ICommand<TResponse> command,
        CancellationToken cancellationToken
    )
    {
        var wrapperType = typeof(ICommandResponseHandlerWrapper<,>).MakeGenericType(command.GetType(), typeof(TResponse));
        var wrapper = (ICommandResponseHandlerWrapper<TResponse>)ServiceProvider.GetRequiredService(wrapperType);
        return wrapper.HandleAsync(command, cancellationToken);
    }
}
