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

/// <summary>
/// Defines a method for pre-processing a command-only or command-response pipeline.
/// </summary>
/// <typeparam name="TCommand">The type of the input value.</typeparam>
public interface ICommandPreProcessor<in TCommand>
    where TCommand : notnull
{
    /// <summary>
    /// Method for processing the command before the handler.
    /// </summary>
    /// <param name="command">The input value to the handler.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask PreProcessAsync(TCommand command, CancellationToken cancellationToken);
}
