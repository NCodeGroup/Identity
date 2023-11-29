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
using NIdentity.OpenId.Mediator.Wrappers;

namespace NIdentity.OpenId.Mediator.Middleware;

internal class StandardCommandMiddleware<TCommand>(
    IServiceProvider serviceProvider,
    IEnumerable<ICommandPreProcessor<TCommand>> preProcessors,
    IEnumerable<ICommandPostProcessor<TCommand>> postProcessors
) : ICommandMiddleware<TCommand>
    where TCommand : ICommand
{
    private IServiceProvider ServiceProvider { get; } = serviceProvider;
    private IEnumerable<ICommandPreProcessor<TCommand>> PreProcessors { get; } = preProcessors;
    private IEnumerable<ICommandPostProcessor<TCommand>> PostProcessors { get; } = postProcessors;

    public async ValueTask HandleAsync(
        TCommand command,
        CommandMiddlewareDelegate next,
        CancellationToken cancellationToken)
    {
        try
        {
            await PreProcessAsync(
                command,
                cancellationToken);

            await next();

            await PostProcessAsync(
                command,
                cancellationToken);
        }
        catch (Exception exception)
        {
            await NotifyExceptionAsync(
                command,
                exception,
                cancellationToken);

            var state = await HandleExceptionAsync(
                command,
                exception,
                cancellationToken);

            if (!state.IsHandled) throw;
        }
    }

    private async ValueTask PreProcessAsync(
        TCommand command,
        CancellationToken cancellationToken)
    {
        foreach (var processor in PreProcessors)
        {
            await processor.PreProcessAsync(
                command,
                cancellationToken);
        }
    }

    private async ValueTask PostProcessAsync(
        TCommand command,
        CancellationToken cancellationToken)
    {
        foreach (var processor in PostProcessors)
        {
            await processor.PostProcessAsync(
                command,
                cancellationToken);
        }
    }

    private async ValueTask NotifyExceptionAsync(
        TCommand command,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var wrapperType = typeof(CommandExceptionListenerWrapper<,>).MakeGenericType(
            typeof(TCommand),
            exception.GetType());

        var wrapper = (ICommandExceptionListenerWrapper<TCommand>)
            ServiceProvider.GetRequiredService(wrapperType);

        await wrapper.ListenAsync(
            command,
            exception,
            cancellationToken);
    }

    private async ValueTask<CommandExceptionHandlerState> HandleExceptionAsync(
        TCommand command,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var state = new CommandExceptionHandlerState();

        var wrapperType = typeof(CommandExceptionHandlerWrapper<,>).MakeGenericType(
            typeof(TCommand),
            exception.GetType());

        var wrapper = (ICommandExceptionHandlerWrapper<TCommand>)
            ServiceProvider.GetRequiredService(wrapperType);

        await wrapper.HandleAsync(
            command,
            exception,
            state,
            cancellationToken);

        return state;
    }
}
