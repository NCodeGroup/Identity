// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Identity.AspNetCore;
using NCode.Identity.Secrets.KeyWrapping;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Server.Logging;
using NCode.Identity.Server.SecretProtection;
using Xunit;

namespace NCode.Identity.Server.Tests;

public class PlatformSecretProtectorWarmupTests : BaseTests
{
    private ISecretProtector CreateProtector() => CreateStrictMock<ISecretProtector>().Object;

    private ISecretProtectorProvider CreateProvider(
        ISecretProtector protector,
        CancellationToken cancellationToken
    )
    {
        var mockProvider = CreateStrictMock<ISecretProtectorProvider>();
        mockProvider
            .Setup(x =>
                x.GetProtectorAsync(
                    SecretOwner.Platform,
                    It.Is<IReadOnlyCollection<string>>(values => values.Count == 0),
                    cancellationToken
                )
            )
            .Returns(new ValueTask<ISecretProtector>(protector))
            .Verifiable();
        return mockProvider.Object;
    }

    private PlatformSecretProtectorWarmup CreateWarmup(
        ISecretProtectorProvider provider,
        SecretProtectionOptions protectionOptions,
        bool localDevelopmentEnabled,
        PlatformSecretProtectorHolder holder,
        ILogger<PlatformSecretProtectorWarmup> logger
    ) =>
        new(
            // A key ring this code cannot read names no master key, so the loader asks the provider for the
            // platform's protector alone.
            new PlatformSecretProtectorLoader(
                provider,
                new DataProtectionKeyRingReader(Options.Create(new KeyManagementOptions())),
                Options.Create(protectionOptions),
                TimeProvider.System,
                holder,
                CreateLooseMock<ILogger<PlatformSecretProtectorLoader>>().Object
            ),
            Options.Create(protectionOptions),
            new LocalDevelopmentMode(localDevelopmentEnabled),
            logger
        );

    #region StartAsync Tests

    [Fact]
    public async Task StartAsync_LoadsPlatformProtector()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var protector = CreateProtector();
        var holder = new PlatformSecretProtectorHolder();
        var warmup = CreateWarmup(
            CreateProvider(protector, cancellationToken),
            new SecretProtectionOptions(),
            false,
            holder,
            CreateLooseMock<ILogger<PlatformSecretProtectorWarmup>>().Object
        );

        await warmup.StartAsync(cancellationToken);

        Assert.Same(protector, holder.Protector);
        Assert.Same(protector, holder.GetRequired());
    }

    [Fact]
    public async Task StartAsync_DeploymentWithLocalRootKey_LogsWarning()
    {
        var mockLogger = CreateStrictMock<ILogger<PlatformSecretProtectorWarmup>>();
        mockLogger.Setup(x => x.IsEnabled(LogLevel.Warning)).Returns(true).Verifiable();
        mockLogger
            .Setup(x =>
                x.Log(
                    LogLevel.Warning,
                    It.Is<EventId>(eventId => eventId.Id == EventIds.LocalRootKeyInDeployment),
                    It.IsAny<It.IsAnyType>(),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                )
            )
            .Verifiable();
        var warmup = CreateWarmup(
            CreateProvider(CreateProtector(), CancellationToken.None),
            new SecretProtectionOptions { RootKeyProvider = KeyWrapperNames.Local },
            false,
            new PlatformSecretProtectorHolder(),
            mockLogger.Object
        );

        await warmup.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_DeploymentWithoutLocalRootKey_DoesNotLog()
    {
        // Strict logger with no setups: any log call fails the test.
        var warmup = CreateWarmup(
            CreateProvider(CreateProtector(), CancellationToken.None),
            new SecretProtectionOptions { RootKeyProvider = KeyWrapperNames.AwsKms },
            false,
            new PlatformSecretProtectorHolder(),
            CreateStrictMock<ILogger<PlatformSecretProtectorWarmup>>().Object
        );

        await warmup.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_LocalRootKeyWithLocalDevelopmentEnabled_DoesNotLog()
    {
        // Strict logger with no setups: any log call fails the test.
        var warmup = CreateWarmup(
            CreateProvider(CreateProtector(), CancellationToken.None),
            new SecretProtectionOptions { RootKeyProvider = KeyWrapperNames.Local },
            true,
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
            new SecretProtectionOptions(),
            false,
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
            "The platform's secret protector is not loaded: the Data Protection key ring was used before the host started its hosted services.",
            exception.Message
        );
    }

    #endregion
}
