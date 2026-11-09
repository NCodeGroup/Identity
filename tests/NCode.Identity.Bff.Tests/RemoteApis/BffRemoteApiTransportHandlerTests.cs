// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using Moq;
using NCode.Identity.Bff.RemoteApis;
using Xunit;

namespace NCode.Identity.Bff.Tests.RemoteApis;

public class BffRemoteApiTransportHandlerTests : BaseTests
{
    #region SendAsync Tests

    [Fact]
    public async Task SendAsync_SendsThroughTheRemoteApiClientsHandler()
    {
        var stub = StubHttpMessageHandler.Respond(HttpStatusCode.NoContent);
        var handlerFactory = CreateStrictMock<IHttpMessageHandlerFactory>();
        handlerFactory
            .Setup(x => x.CreateHandler(BffDefaults.RemoteApiHttpClientName))
            .Returns(stub)
            .Verifiable();
        using var transport = new BffRemoteApiTransportHandler(handlerFactory.Object);
        using var invoker = new HttpMessageInvoker(transport);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://api.example.test/orders"
        );

        using var response = await invoker.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var recorded = Assert.Single(stub.Requests);
        Assert.Equal(new Uri("https://api.example.test/orders"), recorded.RequestUri);
    }

    [Fact]
    public async Task SendAsync_AsksTheFactoryForEveryRequest()
    {
        var stub = StubHttpMessageHandler.Respond(HttpStatusCode.NoContent);
        var handlerFactory = CreateStrictMock<IHttpMessageHandlerFactory>();
        handlerFactory
            .Setup(x => x.CreateHandler(BffDefaults.RemoteApiHttpClientName))
            .Returns(stub)
            .Verifiable();
        using var transport = new BffRemoteApiTransportHandler(handlerFactory.Object);
        using var invoker = new HttpMessageInvoker(transport);

        for (var i = 0; i < 2; i++)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                "https://api.example.test/orders"
            );
            using var response = await invoker.SendAsync(request, CancellationToken.None);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        handlerFactory.Verify(
            x => x.CreateHandler(BffDefaults.RemoteApiHttpClientName),
            Times.Exactly(2)
        );
    }

    #endregion
}
