#region Copyright Preamble
//
//    Copyright @ 2026 NCode Group
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

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Identity.OpenId;
using NCode.Identity.Secrets.KeyWrapping;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Server.Logging;
using NCode.Identity.Server.SecretProtection;
using Xunit;

namespace NCode.Identity.Server.Tests;

public class ServerSecretProtectorWarmupTests : BaseTests
{
    private const string ServerId = "server-a";

    private ISecretProtector CreateProtector() => CreateStrictMock<ISecretProtector>().Object;

    private IHostEnvironment CreateEnvironment(string environmentName)
    {
        var mockEnvironment = CreateStrictMock<IHostEnvironment>();
        mockEnvironment.Setup(x => x.EnvironmentName).Returns(environmentName).Verifiable();
        return mockEnvironment.Object;
    }

    private ISecretProtectorProvider CreateProvider(
        string expectedServerId,
        ISecretProtector protector,
        CancellationToken cancellationToken
    )
    {
        var mockProvider = CreateStrictMock<ISecretProtectorProvider>();
        mockProvider
            .Setup(x =>
                x.GetProtectorAsync(
                    SecretOwner.ForServer(expectedServerId),
                    It.Is<IReadOnlyCollection<string>>(values => values.Count == 0),
                    cancellationToken
                )
            )
            .Returns(new ValueTask<ISecretProtector>(protector))
            .Verifiable();
        return mockProvider.Object;
    }

    private static OpenIdOptions CreateOpenIdOptions(string serverId) =>
        new() { Server = new OpenIdServerOptions { ServerId = serverId } };

    private static ServerSecretProtectorWarmup CreateWarmup(
        ISecretProtectorProvider provider,
        OpenIdOptions openIdOptions,
        SecretProtectionOptions protectionOptions,
        IHostEnvironment hostEnvironment,
        ServerSecretProtectorHolder holder,
        ILogger<ServerSecretProtectorWarmup> logger
    ) =>
        new(
            provider,
            Options.Create(openIdOptions),
            Options.Create(protectionOptions),
            hostEnvironment,
            holder,
            logger
        );

    #region StartAsync Tests

    [Fact]
    public async Task StartAsync_LoadsServerProtector()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var protector = CreateProtector();
        var holder = new ServerSecretProtectorHolder();
        var warmup = CreateWarmup(
            CreateProvider(ServerId, protector, cancellationToken),
            CreateOpenIdOptions(ServerId),
            new SecretProtectionOptions(),
            CreateEnvironment(Environments.Development),
            holder,
            CreateLooseMock<ILogger<ServerSecretProtectorWarmup>>().Object
        );

        await warmup.StartAsync(cancellationToken);

        Assert.Same(protector, holder.Protector);
        Assert.Same(protector, holder.GetRequired());
    }

    [Fact]
    public async Task StartAsync_NoServerIdConfigured_UsesDefaultServerId()
    {
        var protector = CreateProtector();
        var holder = new ServerSecretProtectorHolder();
        var warmup = CreateWarmup(
            CreateProvider(OpenIdServerOptions.DefaultServerId, protector, CancellationToken.None),
            CreateOpenIdOptions(string.Empty),
            new SecretProtectionOptions(),
            CreateEnvironment(Environments.Development),
            holder,
            CreateLooseMock<ILogger<ServerSecretProtectorWarmup>>().Object
        );

        await warmup.StartAsync(CancellationToken.None);

        Assert.Same(protector, holder.Protector);
    }

    [Fact]
    public async Task StartAsync_ProductionWithLocalRootKey_LogsWarning()
    {
        var mockLogger = CreateStrictMock<ILogger<ServerSecretProtectorWarmup>>();
        mockLogger.Setup(x => x.IsEnabled(LogLevel.Warning)).Returns(true).Verifiable();
        mockLogger
            .Setup(x =>
                x.Log(
                    LogLevel.Warning,
                    It.Is<EventId>(eventId => eventId.Id == EventIds.LocalRootKeyInProduction),
                    It.IsAny<It.IsAnyType>(),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                )
            )
            .Verifiable();
        var warmup = CreateWarmup(
            CreateProvider(ServerId, CreateProtector(), CancellationToken.None),
            CreateOpenIdOptions(ServerId),
            new SecretProtectionOptions { RootKeyProvider = KeyWrapperNames.Local },
            CreateEnvironment(Environments.Production),
            new ServerSecretProtectorHolder(),
            mockLogger.Object
        );

        await warmup.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_ProductionWithoutLocalRootKey_DoesNotLog()
    {
        // Strict logger with no setups: any log call fails the test.
        var warmup = CreateWarmup(
            CreateProvider(ServerId, CreateProtector(), CancellationToken.None),
            CreateOpenIdOptions(ServerId),
            new SecretProtectionOptions { RootKeyProvider = KeyWrapperNames.AwsKms },
            CreateEnvironment(Environments.Production),
            new ServerSecretProtectorHolder(),
            CreateStrictMock<ILogger<ServerSecretProtectorWarmup>>().Object
        );

        await warmup.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_LocalRootKeyOutsideProduction_DoesNotLog()
    {
        // Strict logger with no setups: any log call fails the test.
        var warmup = CreateWarmup(
            CreateProvider(ServerId, CreateProtector(), CancellationToken.None),
            CreateOpenIdOptions(ServerId),
            new SecretProtectionOptions { RootKeyProvider = KeyWrapperNames.Local },
            CreateEnvironment(Environments.Development),
            new ServerSecretProtectorHolder(),
            CreateStrictMock<ILogger<ServerSecretProtectorWarmup>>().Object
        );

        await warmup.StartAsync(CancellationToken.None);
    }

    #endregion

    #region StopAsync Tests

    [Fact]
    public async Task StopAsync_Completes()
    {
        var warmup = CreateWarmup(
            CreateStrictMock<ISecretProtectorProvider>().Object,
            CreateOpenIdOptions(ServerId),
            new SecretProtectionOptions(),
            CreateStrictMock<IHostEnvironment>().Object,
            new ServerSecretProtectorHolder(),
            CreateStrictMock<ILogger<ServerSecretProtectorWarmup>>().Object
        );

        await warmup.StopAsync(CancellationToken.None);
    }

    #endregion

    #region GetRequired Tests

    [Fact]
    public void GetRequired_NotLoaded_Throws()
    {
        var holder = new ServerSecretProtectorHolder();

        var exception = Assert.Throws<InvalidOperationException>(() => holder.GetRequired());

        Assert.Equal(
            "The server's secret protector is not loaded: the Data Protection key ring was used before the host started its hosted services.",
            exception.Message
        );
    }

    #endregion
}
