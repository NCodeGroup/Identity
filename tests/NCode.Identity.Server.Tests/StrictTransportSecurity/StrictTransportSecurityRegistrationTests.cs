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

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace NCode.Identity.Server.Tests.StrictTransportSecurity;

public sealed class StrictTransportSecurityRegistrationTests : BaseTests
{
    #region UseStrictTransportSecurity Tests

    [Fact]
    public async Task UseStrictTransportSecurity_OverHttpsOutsideDevelopment_SendsTheDefaultPolicy()
    {
        var header = await SendAsync([], Environments.Production, "https", "id.example.com");

        Assert.Equal("max-age=31536000; includeSubDomains", header);
    }

    [Fact]
    public async Task UseStrictTransportSecurity_WithPreload_SendsPreload()
    {
        var header = await SendAsync(
            new Dictionary<string, string?>
            {
                ["StrictTransportSecurity:MaxAge"] = "730.00:00:00",
                ["StrictTransportSecurity:Preload"] = "true",
            },
            Environments.Production,
            "https",
            "id.example.com"
        );

        Assert.Equal("max-age=63072000; includeSubDomains; preload", header);
    }

    [Fact]
    public async Task UseStrictTransportSecurity_WithoutSubdomains_SendsMaxAgeOnly()
    {
        var header = await SendAsync(
            new Dictionary<string, string?>
            {
                ["StrictTransportSecurity:IncludeSubDomains"] = "false",
            },
            Environments.Production,
            "https",
            "id.example.com"
        );

        Assert.Equal("max-age=31536000", header);
    }

    [Fact]
    public async Task UseStrictTransportSecurity_InDevelopment_SendsNothing()
    {
        var header = await SendAsync([], Environments.Development, "https", "id.example.com");

        Assert.Null(header);
    }

    [Fact]
    public async Task UseStrictTransportSecurity_WhenDisabled_SendsNothing()
    {
        var header = await SendAsync(
            new Dictionary<string, string?> { ["StrictTransportSecurity:Enabled"] = "false" },
            Environments.Production,
            "https",
            "id.example.com"
        );

        Assert.Null(header);
    }

    [Fact]
    public async Task UseStrictTransportSecurity_OverHttp_SendsNothing()
    {
        var header = await SendAsync([], Environments.Production, "http", "id.example.com");

        Assert.Null(header);
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("127.0.0.1")]
    [InlineData("[::1]")]
    public async Task UseStrictTransportSecurity_ToALoopbackHost_SendsNothing(string host)
    {
        var header = await SendAsync([], Environments.Production, "https", host);

        Assert.Null(header);
    }

    [Fact]
    public async Task UseStrictTransportSecurity_ToAnExcludedHost_SendsNothing()
    {
        var header = await SendAsync(
            new Dictionary<string, string?>
            {
                ["StrictTransportSecurity:ExcludedHosts:0"] = "internal.example.com",
            },
            Environments.Production,
            "https",
            "internal.example.com"
        );

        Assert.Null(header);
    }

    #endregion

    #region AddStrictTransportSecurity Tests

    [Fact]
    public async Task AddStrictTransportSecurity_WhenAnOptionIsInvalid_Throws()
    {
        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() =>
            SendAsync(
                new Dictionary<string, string?>
                {
                    ["StrictTransportSecurity:MaxAge"] = "-00:00:01",
                },
                Environments.Production,
                "https",
                "id.example.com"
            )
        );

        Assert.Contains(
            "StrictTransportSecurity:MaxAge",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task AddStrictTransportSecurity_WhenSectionHasUnknownKey_Throws()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SendAsync(
                new Dictionary<string, string?>
                {
                    ["StrictTransportSecurity:MaxAgeSeconds"] = "10",
                },
                Environments.Production,
                "https",
                "id.example.com"
            )
        );

        Assert.Contains("'StrictTransportSecurity'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("'MaxAgeSeconds'", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    private async Task<string?> SendAsync(
        Dictionary<string, string?> configurationData,
        string environmentName,
        string scheme,
        string host
    )
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData)
            .Build();

        var environment = CreateStrictMock<IHostEnvironment>();
        environment.SetupGet(x => x.EnvironmentName).Returns(environmentName).Verifiable();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSingleton(environment.Object);
        serviceCollection.AddStrictTransportSecurity(configuration);

        await using var serviceProvider = serviceCollection.BuildServiceProvider();

        var app = new ApplicationBuilder(serviceProvider);
        app.UseStrictTransportSecurity();
        app.Run(_ => Task.CompletedTask);

        var context = new DefaultHttpContext { RequestServices = serviceProvider };
        context.Request.Scheme = scheme;
        context.Request.Host = new HostString(host);

        await app.Build()(context);

        var value = context.Response.Headers.StrictTransportSecurity;
        return value.Count == 0 ? null : value.ToString();
    }
}
