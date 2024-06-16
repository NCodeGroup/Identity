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

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Mediator.Middleware;

/// <summary>
/// Defines a method for post-processing a command-response pipeline.
/// </summary>
/// <typeparam name="TCommand">The type of the input value.</typeparam>
/// <typeparam name="TResponse">The type of the return value.</typeparam>
[PublicAPI]
public interface ICommandResponsePostProcessor<in TCommand, in TResponse>
    where TCommand : ICommand<TResponse>
{
    /// <summary>
    /// Method for processing the command and response after the handler.
    /// </summary>
    /// <param name="command">The input value to the handler.</param>
    /// <param name="response">The return value from the handler.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask PostProcessAsync(
        TCommand command,
        TResponse response,
        CancellationToken cancellationToken);
}
