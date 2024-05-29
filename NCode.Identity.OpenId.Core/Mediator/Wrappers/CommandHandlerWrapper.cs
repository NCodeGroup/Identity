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

internal interface ICommandHandlerWrapper<in TCommand>
    where TCommand : ICommand
{
    ValueTask HandleAsync(
        TCommand command,
        CancellationToken cancellationToken);
}

internal interface ICommandHandlerWrapper
{
    ValueTask HandleAsync(
        ICommand command,
        CancellationToken cancellationToken);
}

internal class CommandHandlerWrapper<TCommand> : ICommandHandlerWrapper<TCommand>, ICommandHandlerWrapper
    where TCommand : ICommand
{
    private MiddlewareChainDelegate MiddlewareChain { get; }

    private delegate ValueTask MiddlewareChainDelegate(
        TCommand command,
        CancellationToken cancellationToken);

    public CommandHandlerWrapper(
        IEnumerable<ICommandHandler<TCommand>> handlers,
        IEnumerable<ICommandMiddleware<TCommand>> middlewares)
    {
        MiddlewareChain = middlewares.Select(WrapMiddleware).Aggregate(
            WrapRoot(handlers),
            (next, factory) => factory(next));
    }

    private static MiddlewareChainDelegate WrapRoot(
        IEnumerable<ICommandHandler<TCommand>> handlers) =>
        async (command, cancellationToken) =>
        {
            foreach (var handler in handlers)
            {
                await handler.HandleAsync(command, cancellationToken);
            }
        };

    private static Func<MiddlewareChainDelegate, MiddlewareChainDelegate> WrapMiddleware(
        ICommandMiddleware<TCommand> middleware) =>
        next => (command, cancellationToken) =>
        {
            return middleware.HandleAsync(command, SimpleNextAsync, cancellationToken);
            ValueTask SimpleNextAsync() => next(command, cancellationToken);
        };

    public ValueTask HandleAsync(TCommand command, CancellationToken cancellationToken) =>
        MiddlewareChain(command, cancellationToken);

    public ValueTask HandleAsync(ICommand command, CancellationToken cancellationToken) =>
        MiddlewareChain((TCommand)command, cancellationToken);
}
