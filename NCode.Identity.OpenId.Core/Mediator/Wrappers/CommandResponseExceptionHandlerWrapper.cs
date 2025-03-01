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

using NCode.Identity.OpenId.Mediator.Middleware;

namespace NCode.Identity.OpenId.Mediator.Wrappers;

internal interface ICommandResponseExceptionHandlerWrapper<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    ValueTask HandleAsync(
        TCommand command,
        Exception exception,
        CommandResponseExceptionHandlerState<TResponse> state,
        CancellationToken cancellationToken);
}

internal class CommandResponseExceptionHandlerWrapper<TCommand, TException, TResponse>(
    IEnumerable<ICommandResponseExceptionHandler<TCommand, TException, TResponse>> handlers
) : ICommandResponseExceptionHandlerWrapper<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
    where TException : Exception
{
    private IEnumerable<ICommandResponseExceptionHandler<TCommand, TException, TResponse>> Handlers { get; } = handlers;

    private static IEnumerable<T> Sort<T>(IEnumerable<T> collection) =>
        collection.OrderByDescending(item => item is ISupportMediatorPriority support ? support.MediatorPriority : 0);

    public async ValueTask HandleAsync(
        TCommand command,
        Exception exception,
        CommandResponseExceptionHandlerState<TResponse> state,
        CancellationToken cancellationToken)
    {
        foreach (var handler in Sort(Handlers))
        {
            await handler.HandleAsync(
                command,
                (TException)exception,
                state,
                cancellationToken);

            if (state.IsHandled) return;
        }
    }
}
