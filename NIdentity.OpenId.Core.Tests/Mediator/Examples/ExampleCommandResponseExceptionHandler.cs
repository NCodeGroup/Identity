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

using NIdentity.OpenId.Mediator;
using NIdentity.OpenId.Mediator.Middleware;

namespace NIdentity.OpenId.Core.Tests.Mediator.Examples;

internal interface IExampleCommandResponseExceptionHandler<TResponse> :
    ICommandResponseExceptionHandler<ICommand<TResponse>, Exception, TResponse>
{
    // nothing
}

internal class ExampleCommandResponseExceptionHandler<TCommand, TException, TResponse> :
    ICommandResponseExceptionHandler<TCommand, TException, TResponse>
    where TCommand : ICommand<TResponse>
    where TException : Exception
{
    private IExampleCommandResponseExceptionHandler<TResponse> Handler { get; }

    public ExampleCommandResponseExceptionHandler(IExampleCommandResponseExceptionHandler<TResponse> handler) =>
        Handler = handler;

    public async ValueTask HandleAsync(
        TCommand command,
        TException exception,
        CommandResponseExceptionHandlerState<TResponse> state,
        CancellationToken cancellationToken) =>
        await Handler.HandleAsync(
            command,
            exception,
            state,
            cancellationToken);
}
