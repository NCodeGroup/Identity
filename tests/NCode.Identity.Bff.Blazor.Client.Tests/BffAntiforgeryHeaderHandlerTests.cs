// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NCode.Identity.Bff.Blazor.Client.Options;
using Xunit;

namespace NCode.Identity.Bff.Blazor.Client.Tests;

public class BffAntiforgeryHeaderHandlerTests : BaseTests
{
    private const string UserDocument = """
        {
          "sub": "user-1",
          "logoutUrl": "/bff/logout",
          "logoutSid": "guard-1"
        }
        """;

    private static DateTimeOffset Start { get; } = new(2023, 11, 14, 22, 13, 20, TimeSpan.Zero);

    private static TimeSpan Timeout { get; } = TimeSpan.FromSeconds(10);

    private BffClientSession CreateIdleSession() =>
        CreateSession(CreateStrictMock<IHttpClientFactory>().Object);

    private BffClientSession CreateProbingSession(StubHttpMessageHandler probeHandler)
    {
        var httpClientFactory = CreateStrictMock<IHttpClientFactory>();
        httpClientFactory
            .Setup(x => x.CreateClient(BffBlazorClientDefaults.HttpClientName))
            .Returns(() => new HttpClient(probeHandler, disposeHandler: false))
            .Verifiable();

        return CreateSession(httpClientFactory.Object);
    }

    private static BffClientSession CreateSession(IHttpClientFactory httpClientFactory) =>
        new(
            httpClientFactory,
            new TestNavigationManager(),
            Microsoft.Extensions.Options.Options.Create(new BffBlazorClientOptions()),
            new FakeTimeProvider(Start),
            CreateLooseMock<ILogger<BffClientSession>>().Object
        );

    private static HttpMessageInvoker CreateInvoker(
        BffClientSession session,
        StubHttpMessageHandler inner
    ) =>
        new(
            new BffAntiforgeryHeaderHandler(
                new TestNavigationManager(),
                session,
                Microsoft.Extensions.Options.Options.Create(new BffBlazorClientOptions())
            )
            {
                InnerHandler = inner,
            }
        );

    #region SendAsync Tests

    [Fact]
    public async Task SendAsync_SameOrigin_AddsHeader()
    {
        using var session = CreateIdleSession();
        var inner = StubHttpMessageHandler.Respond(HttpStatusCode.OK);
        using var invoker = CreateInvoker(session, inner);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://app.example/api/data");
        request.Headers.TryAddWithoutValidation("X-CSRF", "stale");

        using var response = await invoker.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sent = Assert.Single(inner.Requests);
        Assert.Equal("1", Assert.Single(sent.Headers.GetValues("X-CSRF")));
    }

    [Fact]
    public async Task SendAsync_OtherOrigin_LeavesRequest()
    {
        using var session = CreateIdleSession();
        var inner = StubHttpMessageHandler.Respond(HttpStatusCode.Unauthorized);
        using var invoker = CreateInvoker(session, inner);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://other.example/api/data"
        );

        using var response = await invoker.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var sent = Assert.Single(inner.Requests);
        Assert.False(sent.Headers.Contains("X-CSRF"));
    }

    [Fact]
    public async Task SendAsync_SameOrigin401_Invalidates()
    {
        var probed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var probeHandler = new StubHttpMessageHandler(
            (_, _) =>
            {
                probed.TrySetResult();
                return Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent.Create(JsonElement.Parse(UserDocument)),
                    }
                );
            }
        );
        using var session = CreateProbingSession(probeHandler);
        Assert.True(BffClientUserReader.TryRead(JsonElement.Parse(UserDocument), out var user));
        Assert.NotNull(user);
        session.Seed(user);
        await session.GetAuthenticationStateAsync().WaitAsync(Timeout);
        Assert.Empty(probeHandler.Requests);
        var inner = StubHttpMessageHandler.Respond(HttpStatusCode.Unauthorized);
        using var invoker = CreateInvoker(session, inner);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://app.example/api/data");

        using var response = await invoker.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await probed.Task.WaitAsync(Timeout);
        Assert.Equal(
            new Uri("https://app.example/bff/user"),
            Assert.Single(probeHandler.Requests).RequestUri
        );
    }

    #endregion
}
