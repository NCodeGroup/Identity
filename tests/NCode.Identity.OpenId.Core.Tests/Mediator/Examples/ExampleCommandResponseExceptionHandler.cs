// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Mediator;
using NCode.Mediator.Middleware;

namespace NCode.Identity.OpenId.Core.Tests.Mediator.Examples;

internal interface IExampleCommandResponseExceptionHandler<TResponse>
    : ICommandResponseExceptionHandler<ICommand<TResponse>, Exception, TResponse>
{
    // nothing
}

internal class ExampleCommandResponseExceptionHandler<TCommand, TException, TResponse>
    : ICommandResponseExceptionHandler<TCommand, TException, TResponse>
    where TCommand : ICommand<TResponse>
    where TException : Exception
{
    private IExampleCommandResponseExceptionHandler<TResponse> Handler { get; }

    public ExampleCommandResponseExceptionHandler(
        IExampleCommandResponseExceptionHandler<TResponse> handler
    ) => Handler = handler;

    public async ValueTask HandleAsync(
        TCommand command,
        TException exception,
        CommandResponseExceptionHandlerState<TResponse> state,
        CancellationToken cancellationToken
    ) => await Handler.HandleAsync(command, exception, state, cancellationToken);
}
