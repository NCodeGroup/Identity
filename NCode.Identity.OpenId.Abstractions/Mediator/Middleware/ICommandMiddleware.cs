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
/// Represents a function that can process the remaining middleware in the command-only pipeline.
/// </summary>
/// <remarks>
/// Declared without arguments so that <see cref="ICommandMiddleware{TCommand}"/> can be contravariant in DI.
/// </remarks>
[PublicAPI]
public delegate ValueTask CommandMiddlewareDelegate();

/// <summary>
/// Defines a middleware component that can be added to a command-only pipeline.
/// </summary>
/// <typeparam name="TCommand">The type of the input value.</typeparam>
[PublicAPI]
public interface ICommandMiddleware<in TCommand>
    where TCommand : ICommand
{
    /// <summary>
    /// Middleware method handler.
    /// </summary>
    /// <param name="command">The input value to handle.</param>
    /// <param name="next">The delegate representing the remaining middleware in the request pipeline.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask HandleAsync(
        TCommand command,
        CommandMiddlewareDelegate next,
        CancellationToken cancellationToken);
}
