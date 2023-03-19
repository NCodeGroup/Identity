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

internal class RequestResponseMiddleware<TRequest, TResponse> : IRequestResponseMiddleware<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private IEnumerable<IRequestPreProcessor<TRequest>> PreProcessors { get; }
    private IEnumerable<IRequestResponsePostProcessor<TRequest, TResponse>> PostProcessors { get; }

    public RequestResponseMiddleware(IEnumerable<IRequestPreProcessor<TRequest>> preProcessors, IEnumerable<IRequestResponsePostProcessor<TRequest, TResponse>> postProcessors)
    {
        PreProcessors = preProcessors;
        PostProcessors = postProcessors;
    }

    public async ValueTask<TResponse> HandleAsync(TRequest request, RequestResponseMiddlewareDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        foreach (var processor in PreProcessors)
        {
            await processor.PreProcessAsync(request, cancellationToken);
        }

        // TODO: add an exception processor
        var response = await next();

        foreach (var processor in PostProcessors)
        {
            await processor.PostProcessAsync(request, response, cancellationToken);
        }

        return response;
    }
}
