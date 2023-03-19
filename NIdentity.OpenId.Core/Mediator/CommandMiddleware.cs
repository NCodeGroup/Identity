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

internal class CommandMiddleware<TCommand> : ICommandMiddleware<TCommand>
    where TCommand : ICommand
{
    private IEnumerable<ICommandPreProcessor<TCommand>> PreProcessors { get; }
    private IEnumerable<ICommandPostProcessor<TCommand>> PostProcessors { get; }

    public CommandMiddleware(IEnumerable<ICommandPreProcessor<TCommand>> preProcessors, IEnumerable<ICommandPostProcessor<TCommand>> postProcessors)
    {
        PreProcessors = preProcessors;
        PostProcessors = postProcessors;
    }

    public async ValueTask HandleAsync(TCommand command, CommandMiddlewareDelegate next, CancellationToken cancellationToken)
    {
        foreach (var processor in PreProcessors)
        {
            await processor.PreProcessAsync(command, cancellationToken);
        }

        // TODO: add an exception processor
        await next();

        foreach (var processor in PostProcessors)
        {
            await processor.PostProcessAsync(command, cancellationToken);
        }
    }
}
