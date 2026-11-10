// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using NCode.Identity.Bff.Tokens;
using NCode.Identity.Events;
using NCode.Identity.Jose;
using NCode.Identity.Jwt;
using NCode.Identity.OpenId.Client;
using NCode.Identity.Secrets;
using NCode.Identity.SharedState;
using Xunit;

namespace NCode.Identity.Bff.Tests.Tokens;

public class BffHttpClientBuilderExtensionsTests
{
    private const string ClientName = "api";

    #region AddBffAccessTokenHandler Tests

    [Fact]
    public async Task AddBffAccessTokenHandler_AddsTheHandlerToTheChain()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        var clientBuilder = serviceCollection
            .AddSecretsLibrary()
            .AddJoseLibrary()
            .AddJwtLibrary()
            .AddOpenIdClientLibrary();
        var eventsBuilder = serviceCollection.AddEventServices();
        serviceCollection
            .AddSharedStateServices()
            .AddInProcessExpiringKeySet()
            .AddInProcessExpiringValueSet()
            .AddInProcessExpiringIndexSet();
        serviceCollection.AddSingleton(new Mock<IHostEnvironment>().Object);
        clientBuilder.AddBffLibrary(eventsBuilder);
        serviceCollection
            .AddHttpClient(ClientName)
            .AddBffAccessTokenHandler(new BffTokenRequest("user"));

        await using var provider = serviceCollection.BuildServiceProvider();

        var factory = provider.GetRequiredService<IHttpMessageHandlerFactory>();
        var handler = factory.CreateHandler(ClientName);
        var found = false;
        for (
            var current = handler as DelegatingHandler;
            current is not null;
            current = current.InnerHandler as DelegatingHandler
        )
        {
            if (current is BffAccessTokenHandler)
                found = true;
        }
        Assert.True(found);
    }

    [Fact]
    public void AddBffAccessTokenHandler_WithNullRequest_Throws()
    {
        var builder = new ServiceCollection().AddHttpClient(ClientName);

        Assert.Throws<ArgumentNullException>(() => builder.AddBffAccessTokenHandler(null!));
    }

    #endregion
}
