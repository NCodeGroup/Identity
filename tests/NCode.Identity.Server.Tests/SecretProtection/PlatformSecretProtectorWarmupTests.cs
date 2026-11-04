// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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

public class PlatformSecretProtectorWarmupTests : BaseTests
{
    private const string PlatformId = "server-a";

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
                    SecretOwner.ForPlatform(expectedServerId),
                    It.Is<IReadOnlyCollection<string>>(values => values.Count == 0),
                    cancellationToken
                )
            )
            .Returns(new ValueTask<ISecretProtector>(protector))
            .Verifiable();
        return mockProvider.Object;
    }

    private static OpenIdOptions CreateOpenIdOptions(string platformId) =>
        new() { Platform = new OpenIdPlatformOptions { PlatformId = platformId } };

    private static PlatformSecretProtectorWarmup CreateWarmup(
        ISecretProtectorProvider provider,
        OpenIdOptions openIdOptions,
        SecretProtectionOptions protectionOptions,
        IHostEnvironment hostEnvironment,
        PlatformSecretProtectorHolder holder,
        ILogger<PlatformSecretProtectorWarmup> logger
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
        var holder = new PlatformSecretProtectorHolder();
        var warmup = CreateWarmup(
            CreateProvider(PlatformId, protector, cancellationToken),
            CreateOpenIdOptions(PlatformId),
            new SecretProtectionOptions(),
            CreateEnvironment(Environments.Development),
            holder,
            CreateLooseMock<ILogger<PlatformSecretProtectorWarmup>>().Object
        );

        await warmup.StartAsync(cancellationToken);

        Assert.Same(protector, holder.Protector);
        Assert.Same(protector, holder.GetRequired());
    }

    [Fact]
    public async Task StartAsync_NoServerIdConfigured_UsesDefaultServerId()
    {
        var protector = CreateProtector();
        var holder = new PlatformSecretProtectorHolder();
        var warmup = CreateWarmup(
            CreateProvider(
                OpenIdPlatformOptions.DefaultPlatformId,
                protector,
                CancellationToken.None
            ),
            CreateOpenIdOptions(string.Empty),
            new SecretProtectionOptions(),
            CreateEnvironment(Environments.Development),
            holder,
            CreateLooseMock<ILogger<PlatformSecretProtectorWarmup>>().Object
        );

        await warmup.StartAsync(CancellationToken.None);

        Assert.Same(protector, holder.Protector);
    }

    [Fact]
    public async Task StartAsync_ProductionWithLocalRootKey_LogsWarning()
    {
        var mockLogger = CreateStrictMock<ILogger<PlatformSecretProtectorWarmup>>();
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
            CreateProvider(PlatformId, CreateProtector(), CancellationToken.None),
            CreateOpenIdOptions(PlatformId),
            new SecretProtectionOptions { RootKeyProvider = KeyWrapperNames.Local },
            CreateEnvironment(Environments.Production),
            new PlatformSecretProtectorHolder(),
            mockLogger.Object
        );

        await warmup.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_ProductionWithoutLocalRootKey_DoesNotLog()
    {
        // Strict logger with no setups: any log call fails the test.
        var warmup = CreateWarmup(
            CreateProvider(PlatformId, CreateProtector(), CancellationToken.None),
            CreateOpenIdOptions(PlatformId),
            new SecretProtectionOptions { RootKeyProvider = KeyWrapperNames.AwsKms },
            CreateEnvironment(Environments.Production),
            new PlatformSecretProtectorHolder(),
            CreateStrictMock<ILogger<PlatformSecretProtectorWarmup>>().Object
        );

        await warmup.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_LocalRootKeyOutsideProduction_DoesNotLog()
    {
        // Strict logger with no setups: any log call fails the test.
        var warmup = CreateWarmup(
            CreateProvider(PlatformId, CreateProtector(), CancellationToken.None),
            CreateOpenIdOptions(PlatformId),
            new SecretProtectionOptions { RootKeyProvider = KeyWrapperNames.Local },
            CreateEnvironment(Environments.Development),
            new PlatformSecretProtectorHolder(),
            CreateStrictMock<ILogger<PlatformSecretProtectorWarmup>>().Object
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
            CreateOpenIdOptions(PlatformId),
            new SecretProtectionOptions(),
            CreateStrictMock<IHostEnvironment>().Object,
            new PlatformSecretProtectorHolder(),
            CreateStrictMock<ILogger<PlatformSecretProtectorWarmup>>().Object
        );

        await warmup.StopAsync(CancellationToken.None);
    }

    #endregion

    #region GetRequired Tests

    [Fact]
    public void GetRequired_NotLoaded_Throws()
    {
        var holder = new PlatformSecretProtectorHolder();

        var exception = Assert.Throws<InvalidOperationException>(() => holder.GetRequired());

        Assert.Equal(
            "The server's secret protector is not loaded: the Data Protection key ring was used before the host started its hosted services.",
            exception.Message
        );
    }

    #endregion
}
