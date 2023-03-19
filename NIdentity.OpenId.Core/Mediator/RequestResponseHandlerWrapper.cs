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

internal interface IRequestResponseHandlerWrapper<TResponse>
{
    ValueTask<TResponse> HandleAsync(IRequest<TResponse> request, CancellationToken cancellationToken);
}

internal class RequestResponseHandlerWrapper<TRequest, TResponse> : IRequestResponseHandlerWrapper<TResponse>
    where TRequest : IRequest<TResponse>
{
    private MiddlewareChainDelegate MiddlewareChain { get; }

    private delegate ValueTask<TResponse> MiddlewareChainDelegate(TRequest request, CancellationToken cancellationToken);

    public RequestResponseHandlerWrapper(
        IRequestResponseHandler<TRequest, TResponse> handler,
        IEnumerable<IRequestResponseMiddleware<TRequest, TResponse>> middlewares)
    {
        ValueTask<TResponse> RootHandler(TRequest request, CancellationToken token) =>
            handler.HandleAsync(request, token);

        static Func<MiddlewareChainDelegate, MiddlewareChainDelegate> CreateFactory(IRequestResponseMiddleware<TRequest, TResponse> middleware) =>
            next => (request, token) =>
            {
                ValueTask<TResponse> SimpleNext() => next(request, token);
                return middleware.HandleAsync(request, SimpleNext, token);
            };

        MiddlewareChain = middlewares.Select(CreateFactory).Aggregate(
            (MiddlewareChainDelegate)RootHandler,
            (next, factory) => factory(next));
    }

    public ValueTask<TResponse> HandleAsync(IRequest<TResponse> request, CancellationToken cancellationToken) =>
        MiddlewareChain((TRequest)request, cancellationToken);
}
