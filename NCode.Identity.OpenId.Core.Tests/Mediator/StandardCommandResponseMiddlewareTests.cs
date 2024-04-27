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
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Mediator.Middleware;
using NCode.Identity.OpenId.Mediator.Wrappers;
using NCode.Identity.OpenId.Tests.Mediator.Examples;
using Xunit;

namespace NCode.Identity.OpenId.Tests.Mediator;

public class StandardCommandResponseMiddlewareTests : BaseTests
{
    private IServiceCollection Services { get; } = new ServiceCollection();

    private ServiceProvider? ServiceProviderOrNull { get; set; }

    private IServiceProvider ServiceProvider => ServiceProviderOrNull ??= Services.BuildServiceProvider();

    public StandardCommandResponseMiddlewareTests()
    {
        Services.AddSingleton(typeof(ICommandResponseMiddleware<,>), typeof(StandardCommandResponseMiddleware<,>));
        Services.AddTransient(typeof(CommandExceptionListenerWrapper<,>));
        Services.AddTransient(typeof(CommandResponseExceptionHandlerWrapper<,,>));
    }

    protected override void Dispose(bool disposing)
    {
        ServiceProviderOrNull?.Dispose();
        base.Dispose(disposing);
    }

    private ICommandResponseMiddleware<TCommand, TResponse> CreateMiddleware<TCommand, TResponse>()
        where TCommand : ICommand<TResponse> =>
        ServiceProvider.GetRequiredService<ICommandResponseMiddleware<TCommand, TResponse>>();

    private static CommandResponseMiddlewareDelegate<TResponse> SimpleNext<TResponse>(TResponse response) =>
        () => ValueTask.FromResult(response);

    [Fact]
    public async Task Handle_GivenPreProcessAsync()
    {
        var command = new ExampleCommandWithResponse();
        var response = new ExampleResponse();

        var mockCommandPreProcessor = CreateStrictMock<ICommandPreProcessor<ICommand<ExampleResponse>>>();
        mockCommandPreProcessor
            .Setup(x => x.PreProcessAsync(command, default))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        Services.AddSingleton(typeof(ICommandPreProcessor<ICommand<ExampleResponse>>), mockCommandPreProcessor.Object);

        var middleware = CreateMiddleware<ICommand<ExampleResponse>, ExampleResponse>();
        await middleware.HandleAsync(command, SimpleNext(response), default);
    }

    [Fact]
    public async Task Handle_GivenPostProcessAsync()
    {
        var command = new ExampleCommandWithResponse();
        var response = new ExampleResponse();

        var mockCommandPostProcessor = CreateStrictMock<ICommandResponsePostProcessor<ICommand<ExampleResponse>, ExampleResponse>>();
        mockCommandPostProcessor
            .Setup(x => x.PostProcessAsync(command, response, default))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        Services.AddSingleton(typeof(ICommandResponsePostProcessor<ICommand<ExampleResponse>, ExampleResponse>), mockCommandPostProcessor.Object);

        var middleware = CreateMiddleware<ICommand<ExampleResponse>, ExampleResponse>();
        await middleware.HandleAsync(command, SimpleNext(response), default);
    }

    [Fact]
    public async Task Handle_GivenExceptionListenerAsync()
    {
        var command = new ExampleCommandWithResponse();
        var exception = new InvalidOperationException();

        var mockExceptionListener = CreateStrictMock<IExampleCommandExceptionListener>();
        mockExceptionListener
            .Setup(x => x.ListenAsync(command, exception, default))
            .Callback([AssertionMethod](
                object argCommand,
                Exception argException,
                CancellationToken _) =>
            {
                Assert.Same(command, argCommand);
                Assert.Same(exception, argException);
            })
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        Services.AddSingleton(mockExceptionListener.Object);
        Services.AddSingleton(typeof(ICommandExceptionListener<,>), typeof(ExampleCommandExceptionListener<,>));

        var middleware = CreateMiddleware<ICommand<ExampleResponse>, ExampleResponse>();

        var result = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await middleware.HandleAsync(command, () => throw exception, default)
        );
        Assert.Same(exception, result);
    }

    [Fact]
    public async Task Handle_GivenExceptionHandlerAsync()
    {
        var command = new ExampleCommandWithResponse();
        var exception = new InvalidOperationException();
        var response = new ExampleResponse();

        var mockExceptionHandler = CreateStrictMock<IExampleCommandResponseExceptionHandler<ExampleResponse>>();
        mockExceptionHandler
            .Setup(x => x.HandleAsync(command, exception, It.IsAny<CommandResponseExceptionHandlerState<ExampleResponse>>(), default))
            .Callback([AssertionMethod](
                ICommand<ExampleResponse> argCommand,
                Exception argException,
                CommandResponseExceptionHandlerState<ExampleResponse> argState,
                CancellationToken _) =>
            {
                Assert.Same(command, argCommand);
                Assert.Same(exception, argException);
                argState.SetHandled(response);
            })
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        Services.AddSingleton(mockExceptionHandler.Object);
        Services.AddSingleton(typeof(ICommandResponseExceptionHandler<,,>), typeof(ExampleCommandResponseExceptionHandler<,,>));

        var middleware = CreateMiddleware<ICommand<ExampleResponse>, ExampleResponse>();
        var result = await middleware.HandleAsync(command, () => throw exception, default);
        Assert.Same(response, result);
    }
}
