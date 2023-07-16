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

namespace NIdentity.OpenId.Mediator;

internal interface ICommandHandlerWrapper
{
    ValueTask HandleAsync(ICommand command, CancellationToken cancellationToken);
}

internal class CommandHandlerWrapper<TCommand> : ICommandHandlerWrapper
    where TCommand : ICommand
{
    private MiddlewareChainDelegate MiddlewareChain { get; }

    private delegate ValueTask MiddlewareChainDelegate(TCommand command, CancellationToken cancellationToken);

    public CommandHandlerWrapper(
        ICommandHandler<TCommand> handler,
        IEnumerable<ICommandMiddleware<TCommand>> middlewares)
    {
        ValueTask RootHandler(TCommand command, CancellationToken token) =>
            handler.HandleAsync(command, token);

        static Func<MiddlewareChainDelegate, MiddlewareChainDelegate> CreateFactory(ICommandMiddleware<TCommand> middleware) =>
            next => (command, token) =>
            {
                ValueTask SimpleNext() => next(command, token);
                return middleware.HandleAsync(command, SimpleNext, token);
            };

        MiddlewareChain = middlewares.Select(CreateFactory).Aggregate(
            (MiddlewareChainDelegate)RootHandler,
            (next, factory) => factory(next));
    }

    public ValueTask HandleAsync(ICommand command, CancellationToken cancellationToken) =>
        MiddlewareChain((TCommand)command, cancellationToken);
}
