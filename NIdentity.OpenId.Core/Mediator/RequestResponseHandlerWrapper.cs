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

internal interface ICommandResponseHandlerWrapper<TResponse>
{
    ValueTask<TResponse> HandleAsync(ICommand<TResponse> command, CancellationToken cancellationToken);
}

internal class CommandResponseHandlerWrapper<TCommand, TResponse> : ICommandResponseHandlerWrapper<TResponse>
    where TCommand : ICommand<TResponse>
{
    private MiddlewareChainDelegate MiddlewareChain { get; }

    private delegate ValueTask<TResponse> MiddlewareChainDelegate(TCommand command, CancellationToken cancellationToken);

    public CommandResponseHandlerWrapper(
        ICommandResponseHandler<TCommand, TResponse> handler,
        IEnumerable<ICommandResponseMiddleware<TCommand, TResponse>> middlewares)
    {
        ValueTask<TResponse> RootHandler(TCommand command, CancellationToken token) =>
            handler.HandleAsync(command, token);

        static Func<MiddlewareChainDelegate, MiddlewareChainDelegate> CreateFactory(ICommandResponseMiddleware<TCommand, TResponse> middleware) =>
            next => (command, token) =>
            {
                ValueTask<TResponse> SimpleNext() => next(command, token);
                return middleware.HandleAsync(command, SimpleNext, token);
            };

        MiddlewareChain = middlewares.Select(CreateFactory).Aggregate(
            (MiddlewareChainDelegate)RootHandler,
            (next, factory) => factory(next));
    }

    public ValueTask<TResponse> HandleAsync(ICommand<TResponse> command, CancellationToken cancellationToken) =>
        MiddlewareChain((TCommand)command, cancellationToken);
}
