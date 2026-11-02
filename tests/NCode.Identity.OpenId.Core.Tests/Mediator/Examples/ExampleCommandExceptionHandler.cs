// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Mediator;
using NCode.Mediator.Middleware;

namespace NCode.Identity.OpenId.Core.Tests.Mediator.Examples;

internal interface IExampleCommandExceptionHandler : ICommandExceptionHandler<ICommand, Exception>
{
    // nothing
}

internal class ExampleCommandExceptionHandler<TCommand, TException>
    : ICommandExceptionHandler<TCommand, TException>
    where TCommand : ICommand
    where TException : Exception
{
    private IExampleCommandExceptionHandler Handler { get; }

    public ExampleCommandExceptionHandler(IExampleCommandExceptionHandler handler) =>
        Handler = handler;

    public async ValueTask HandleAsync(
        TCommand command,
        TException exception,
        CommandExceptionHandlerState state,
        CancellationToken cancellationToken
    ) => await Handler.HandleAsync(command, exception, state, cancellationToken);
}
