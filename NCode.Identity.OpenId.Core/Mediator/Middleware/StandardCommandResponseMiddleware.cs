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

namespace NCode.Identity.OpenId.Mediator.Middleware;

internal class StandardCommandResponseMiddleware<TCommand, TResponse>(
    IServiceProvider serviceProvider,
    IEnumerable<ICommandPreProcessor<TCommand>> preProcessors,
    IEnumerable<ICommandResponsePostProcessor<TCommand, TResponse>> postProcessors
) : ICommandResponseMiddleware<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    private IServiceProvider ServiceProvider { get; } = serviceProvider;
    private IEnumerable<ICommandPreProcessor<TCommand>> PreProcessors { get; } = preProcessors;
    private IEnumerable<ICommandResponsePostProcessor<TCommand, TResponse>> PostProcessors { get; } = postProcessors;

    public async ValueTask<TResponse> HandleAsync(
        TCommand command,
        CommandResponseMiddlewareDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        try
        {
            await PreProcessAsync(
                command,
                cancellationToken);

            var response = await next();

            await PostProcessAsync(
                command,
                response,
                cancellationToken);

            return response;
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

            if (state.IsHandled)
                return state.Response;

            throw;
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
        TResponse response,
        CancellationToken cancellationToken)
    {
        foreach (var processor in PostProcessors)
        {
            await processor.PostProcessAsync(
                command,
                response,
                cancellationToken);
        }
    }

    private ValueTask NotifyExceptionAsync(
        TCommand command,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var wrapperType = typeof(CommandExceptionListenerWrapper<,>).MakeGenericType(
            typeof(TCommand),
            exception.GetType());

        var wrapper = (ICommandExceptionListenerWrapper<TCommand>)
            ServiceProvider.GetRequiredService(wrapperType);

        return wrapper.ListenAsync(
            command,
            exception,
            cancellationToken);
    }

    private async ValueTask<CommandResponseExceptionHandlerState<TResponse>> HandleExceptionAsync(
        TCommand command,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var state = new CommandResponseExceptionHandlerState<TResponse>();

        var wrapperType = typeof(CommandResponseExceptionHandlerWrapper<,,>).MakeGenericType(
            typeof(TCommand),
            exception.GetType(),
            typeof(TResponse));

        var wrapper = (ICommandResponseExceptionHandlerWrapper<TCommand, TResponse>)
            ServiceProvider.GetRequiredService(wrapperType);

        await wrapper.HandleAsync(
            command,
            exception,
            state,
            cancellationToken);

        return state;
    }
}
