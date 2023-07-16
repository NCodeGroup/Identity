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

internal class CommandResponseMiddleware<TCommand, TResponse> : ICommandResponseMiddleware<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    private IEnumerable<ICommandPreProcessor<TCommand>> PreProcessors { get; }
    private IEnumerable<ICommandResponsePostProcessor<TCommand, TResponse>> PostProcessors { get; }

    public CommandResponseMiddleware(IEnumerable<ICommandPreProcessor<TCommand>> preProcessors, IEnumerable<ICommandResponsePostProcessor<TCommand, TResponse>> postProcessors)
    {
        PreProcessors = preProcessors;
        PostProcessors = postProcessors;
    }

    public async ValueTask<TResponse> HandleAsync(TCommand command, CommandResponseMiddlewareDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        foreach (var processor in PreProcessors)
        {
            await processor.PreProcessAsync(command, cancellationToken);
        }

        // TODO: add an exception processor
        var response = await next();

        foreach (var processor in PostProcessors)
        {
            await processor.PostProcessAsync(command, response, cancellationToken);
        }

        return response;
    }
}
