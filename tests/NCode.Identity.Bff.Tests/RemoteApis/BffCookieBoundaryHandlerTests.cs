// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using NCode.Identity.Bff.RemoteApis;
using NCode.Identity.Bff.Tokens;
using Xunit;

namespace NCode.Identity.Bff.Tests.RemoteApis;

public sealed class BffCookieBoundaryHandlerTests
{
    private const string OtherSetCookie = "theme=dark; Path=/";

    private string[]? SentCookies { get; set; }

    private HttpMessageInvoker CreateInvoker(params string[] setCookies)
    {
        var upstream = new StubHttpMessageHandler(
            (request, _) =>
            {
                SentCookies = request.Headers.TryGetValues("Cookie", out var values)
                    ? values.ToArray()
                    : null;

                var response = new HttpResponseMessage(HttpStatusCode.OK);
                foreach (var setCookie in setCookies)
                    response.Headers.TryAddWithoutValidation("Set-Cookie", setCookie);
                return Task.FromResult(response);
            }
        );
        return new HttpMessageInvoker(new BffCookieBoundaryHandler { InnerHandler = upstream });
    }

    private static HttpRequestMessage CreateRequest(string? cookie, bool isRemoteApi = false)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://upstream.example.com/");
        if (cookie is not null)
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        if (isRemoteApi)
            request.Options.Set(
                BffAccessTokenHandler.ResolvedTokenKey,
                BffTokenResult.Success(null)
            );
        return request;
    }

    private static string[] GetSetCookies(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values) ? values.ToArray() : [];

    #region SendAsync Tests

    [Fact]
    public async Task SendAsync_OtherRoute_RemovesOnlyTheBffCookies()
    {
        using var invoker = CreateInvoker();
        using var request = CreateRequest(
            "theme=dark; __Host-Http-bff=handle;__Host-Http-bff-login=state; __Host-Http-bff-other=1"
        );

        using var response = await invoker.SendAsync(request, CancellationToken.None);

        Assert.NotNull(SentCookies);
        Assert.Equal("theme=dark; __Host-Http-bff-other=1", Assert.Single(SentCookies));
    }

    [Fact]
    public async Task SendAsync_OtherRouteWithOnlyBffCookies_RemovesTheCookieHeader()
    {
        using var invoker = CreateInvoker();
        using var request = CreateRequest("__Host-Http-bff=handle");

        using var response = await invoker.SendAsync(request, CancellationToken.None);

        Assert.Null(SentCookies);
    }

    [Fact]
    public async Task SendAsync_OtherRouteWithoutBffCookies_KeepsTheCookieHeader()
    {
        using var invoker = CreateInvoker();
        using var request = CreateRequest("theme=dark;  lang=en");

        using var response = await invoker.SendAsync(request, CancellationToken.None);

        Assert.NotNull(SentCookies);
        Assert.Equal("theme=dark;  lang=en", Assert.Single(SentCookies));
    }

    [Fact]
    public async Task SendAsync_OtherRouteWithoutCookies_SendsNoCookieHeader()
    {
        using var invoker = CreateInvoker();
        using var request = CreateRequest(null);

        using var response = await invoker.SendAsync(request, CancellationToken.None);

        Assert.Null(SentCookies);
    }

    [Fact]
    public async Task SendAsync_OtherRoute_DropsOnlyTheBffSetCookies()
    {
        using var invoker = CreateInvoker(
            "__Host-Http-bff=forged; Path=/; Secure",
            OtherSetCookie,
            "__Host-Http-bff-login; Path=/"
        );
        using var request = CreateRequest(null);

        using var response = await invoker.SendAsync(request, CancellationToken.None);

        Assert.Equal([OtherSetCookie], GetSetCookies(response));
    }

    [Fact]
    public async Task SendAsync_OtherRouteWithoutBffSetCookies_KeepsEverySetCookie()
    {
        using var invoker = CreateInvoker(OtherSetCookie, "lang=en");
        using var request = CreateRequest(null);

        using var response = await invoker.SendAsync(request, CancellationToken.None);

        Assert.Equal([OtherSetCookie, "lang=en"], GetSetCookies(response));
    }

    [Fact]
    public async Task SendAsync_RemoteApi_RemovesEveryCookieAndSetCookie()
    {
        using var invoker = CreateInvoker(OtherSetCookie);
        using var request = CreateRequest("theme=dark", isRemoteApi: true);

        using var response = await invoker.SendAsync(request, CancellationToken.None);

        Assert.Null(SentCookies);
        Assert.Empty(GetSetCookies(response));
    }

    #endregion
}
