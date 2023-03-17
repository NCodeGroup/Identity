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

internal class RequestResponseHandlerWrapper<TRequest, TResponse> : IResponseHandlerWrapper<TResponse>
    where TRequest : IRequest, IRequest<TResponse>
{
    private IEnumerable<IRequestHandler<TRequest>> RequestHandlers { get; }
    private IRequestResponseHandler<TRequest, TResponse> ResponseHandler { get; }

    public RequestResponseHandlerWrapper(IEnumerable<IRequestHandler<TRequest>> requestHandlers, IRequestResponseHandler<TRequest, TResponse> responseHandler)
    {
        RequestHandlers = requestHandlers;
        ResponseHandler = responseHandler;
    }

    public async ValueTask<TResponse> HandleAsync(IRequest<TResponse> request, CancellationToken cancellationToken)
    {
        foreach (var handler in RequestHandlers)
        {
            await handler.HandleAsync((TRequest)request, cancellationToken);
        }

        return await ResponseHandler.HandleAsync((TRequest)request, cancellationToken);
    }
}
