#region Copyright Preamble

// Copyright @ 2026 NCode Group
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
        var services = new ServiceCollection();
        services.AddLogging();
        var clientBuilder = services
            .AddSecretsLibrary()
            .AddJoseLibrary()
            .AddJwtLibrary()
            .AddOpenIdClientLibrary();
        var eventsBuilder = services.AddEventServices();
        services
            .AddSharedStateServices()
            .AddInProcessExpiringKeySet()
            .AddInProcessExpiringValueSet()
            .AddInProcessExpiringIndexSet();
        services.AddSingleton(new Mock<IHostEnvironment>().Object);
        clientBuilder.AddBffLibrary(eventsBuilder);
        services.AddHttpClient(ClientName).AddBffAccessTokenHandler(new BffTokenRequest("user"));

        await using var provider = services.BuildServiceProvider();

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
