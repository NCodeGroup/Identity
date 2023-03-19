#region Copyright Preamble

//
//    Copyright @ 2022 NCode Group
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

internal interface IRequestHandlerWrapper
{
    ValueTask HandleAsync(IRequest request, CancellationToken cancellationToken);
}

internal class RequestHandlerWrapper<TRequest> : IRequestHandlerWrapper
    where TRequest : IRequest
{
    private MiddlewareChainDelegate MiddlewareChain { get; }

    private delegate ValueTask MiddlewareChainDelegate(TRequest request, CancellationToken cancellationToken);

    public RequestHandlerWrapper(
        IRequestHandler<TRequest> handler,
        IEnumerable<IRequestMiddleware<TRequest>> middlewares)
    {
        ValueTask RootHandler(TRequest request, CancellationToken token) =>
            handler.HandleAsync(request, token);

        static Func<MiddlewareChainDelegate, MiddlewareChainDelegate> CreateFactory(IRequestMiddleware<TRequest> middleware) =>
            next => (request, token) =>
            {
                ValueTask SimpleNext() => next(request, token);
                return middleware.HandleAsync(request, SimpleNext, token);
            };

        MiddlewareChain = middlewares.Select(CreateFactory).Aggregate(
            (MiddlewareChainDelegate)RootHandler,
            (next, factory) => factory(next));
    }

    public ValueTask HandleAsync(IRequest request, CancellationToken cancellationToken) =>
        MiddlewareChain((TRequest)request, cancellationToken);
}
