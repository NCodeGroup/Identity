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

using NIdentity.OpenId.Mediator.Middleware;

namespace NIdentity.OpenId.Mediator.Wrappers;

internal interface ICommandExceptionHandlerWrapper
{
    ValueTask HandleAsync(
        ICommand command,
        Exception exception,
        CommandExceptionHandlerState state,
        CancellationToken cancellationToken);
}

internal class CommandExceptionHandlerWrapper<TCommand, TException> :
    ICommandExceptionHandlerWrapper
    where TCommand : ICommand
    where TException : Exception
{
    private IEnumerable<ICommandExceptionHandler<TCommand, TException>> Handlers { get; }

    public CommandExceptionHandlerWrapper(
        IEnumerable<ICommandExceptionHandler<TCommand, TException>> handlers)
    {
        Handlers = handlers;
    }

    public async ValueTask HandleAsync(
        ICommand command,
        Exception exception,
        CommandExceptionHandlerState state,
        CancellationToken cancellationToken)
    {
        foreach (var handler in Handlers)
        {
            await handler.HandleAsync(
                (TCommand)command,
                (TException)exception,
                state,
                cancellationToken);

            if (state.IsHandled) return;
        }
    }
}
