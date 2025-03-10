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

internal interface ICommandResponseHandlerWrapper<TResponse>
{
    ValueTask<TResponse> HandleAsync(
        ICommand<TResponse> command,
        CancellationToken cancellationToken);
}

internal interface ICommandResponseHandlerWrapper<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    ValueTask<TResponse> HandleAsync(
        TCommand command,
        CancellationToken cancellationToken);
}

internal class CommandResponseHandlerWrapper<TCommand, TResponse> :
    ICommandResponseHandlerWrapper<TResponse>,
    ICommandResponseHandlerWrapper<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    private MiddlewareChainDelegate MiddlewareChain { get; }

    private delegate ValueTask<TResponse> MiddlewareChainDelegate(
        TCommand command,
        CancellationToken cancellationToken);

    public CommandResponseHandlerWrapper(
        ICommandResponseHandler<TCommand, TResponse> handler,
        IEnumerable<ICommandResponseMiddleware<TCommand, TResponse>> middlewares)
    {
        MiddlewareChain = Sort(middlewares).Select(WrapMiddleware).Aggregate(
            (MiddlewareChainDelegate)handler.HandleAsync,
            (next, factory) => factory(next));
    }

    private static IEnumerable<T> Sort<T>(IEnumerable<T> collection) =>
        collection.OrderByDescending(item => item is ISupportMediatorPriority support ? support.MediatorPriority : 0);

    private static Func<MiddlewareChainDelegate, MiddlewareChainDelegate> WrapMiddleware(
        ICommandResponseMiddleware<TCommand, TResponse> middleware) =>
        next => (command, cancellationToken) =>
        {
            return middleware.HandleAsync(command, SimpleNextAsync, cancellationToken);
            ValueTask<TResponse> SimpleNextAsync() => next(command, cancellationToken);
        };

    public ValueTask<TResponse> HandleAsync(ICommand<TResponse> command, CancellationToken cancellationToken) =>
        MiddlewareChain((TCommand)command, cancellationToken);

    public ValueTask<TResponse> HandleAsync(TCommand command, CancellationToken cancellationToken) =>
        MiddlewareChain(command, cancellationToken);
}
