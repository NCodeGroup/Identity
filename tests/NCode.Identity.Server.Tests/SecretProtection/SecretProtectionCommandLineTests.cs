// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Buffers;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Server.SecretProtection;
using Xunit;

namespace NCode.Identity.Server.Tests;

public class SecretProtectionCommandLineTests : BaseTests
{
    private static SecretProtectionCommandLine Parse(params string[] args)
    {
        Assert.True(SecretProtectionCommandLine.TryParse(args, out var commandLine, out var error));
        Assert.Null(error);
        return Assert.IsType<SecretProtectionCommandLine>(commandLine);
    }

    private static string ParseError(params string[] args)
    {
        Assert.False(
            SecretProtectionCommandLine.TryParse(args, out var commandLine, out var error)
        );
        Assert.Null(commandLine);
        return Assert.IsType<string>(error);
    }

    private static ServiceProvider CreateServices(
        IMasterKeyRewrapper rewrapper,
        IStartupValidator? validator = null
    )
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(rewrapper);
        if (validator is not null)
            services.AddSingleton(validator);

        return services.BuildServiceProvider();
    }

    #region TryParse Tests

    [Theory]
    [InlineData]
    [InlineData("--urls", "http://localhost")]
    [InlineData("rewrap")]
    public void TryParse_ArgsThatAreNotTheCommand_ReturnFalseWithNoError(params string[] args)
    {
        Assert.False(
            SecretProtectionCommandLine.TryParse(args, out var commandLine, out var error)
        );
        Assert.Null(commandLine);
        Assert.Null(error);
    }

    [Fact]
    public void TryParse_Rewrap_HasNoOptionsByDefault()
    {
        var command = Parse("secret-protection", "rewrap");

        Assert.Equal("rewrap", command.Verb);
        Assert.Null(command.RecoveryPrivateKeyPath);
        Assert.Null(command.Owner);
        Assert.False(command.DryRun);
    }

    [Fact]
    public void TryParse_EveryOptionOnRewrapRecovery()
    {
        var command = Parse(
            "secret-protection",
            "rewrap-recovery",
            "--recovery-private-key",
            "/etc/key.pem",
            "--owner",
            "tenant:acme",
            "--dry-run"
        );

        Assert.Equal("rewrap-recovery", command.Verb);
        Assert.Equal("/etc/key.pem", command.RecoveryPrivateKeyPath);
        Assert.Equal(SecretOwner.ForTenant("acme"), command.Owner);
        Assert.True(command.DryRun);
    }

    [Fact]
    public void TryParse_OptionsInAnyOrder()
    {
        var command = Parse(
            "secret-protection",
            "rewrap",
            "--dry-run",
            "--owner",
            "server:default"
        );

        Assert.True(command.DryRun);
        Assert.Equal(SecretOwner.ForServer("default"), command.Owner);
    }

    [Theory]
    [InlineData("secret-protection")]
    [InlineData("secret-protection", "destroy")]
    [InlineData("secret-protection", "rewrap", "--force")]
    [InlineData("secret-protection", "rewrap", "--owner")]
    [InlineData("secret-protection", "rewrap", "--owner", "--dry-run")]
    [InlineData("secret-protection", "rewrap", "--recovery-private-key")]
    [InlineData("secret-protection", "rewrap", "--dry-run", "--dry-run")]
    [InlineData("secret-protection", "rewrap", "--owner", "tenant:a", "--owner", "tenant:b")]
    [InlineData(
        "secret-protection",
        "rewrap",
        "--recovery-private-key",
        "a",
        "--recovery-private-key",
        "b"
    )]
    [InlineData("secret-protection", "rewrap", "--owner", "acme")]
    [InlineData("secret-protection", "rewrap", "--owner", "tenant:")]
    [InlineData("secret-protection", "rewrap", "extra")]
    public void TryParse_InvalidArgs_ReturnAnError(params string[] args)
    {
        Assert.False(string.IsNullOrWhiteSpace(ParseError(args)));
    }

    [Fact]
    public void Usage_NamesTheVerbsAndOptions()
    {
        Assert.Contains(
            "rewrap-recovery",
            SecretProtectionCommandLine.Usage,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "--recovery-private-key",
            SecretProtectionCommandLine.Usage,
            StringComparison.Ordinal
        );
    }

    #endregion

    #region RunAsync Tests

    [Fact]
    public async Task RunAsync_NoFailures_ReturnsZeroAndPassesTheOptionsToTheRewrapper()
    {
        MasterKeyRewrapRequest? captured = null;
        var rewrapper = CreateStrictMock<IMasterKeyRewrapper>();
        rewrapper
            .Setup(x =>
                x.RewrapAsync(It.IsAny<MasterKeyRewrapRequest>(), It.IsAny<CancellationToken>())
            )
            .Callback<MasterKeyRewrapRequest, CancellationToken>((r, _) => captured = r)
            .Returns(new ValueTask<MasterKeyRewrapResult>(new MasterKeyRewrapResult(2, 2, 0, 0)));
        using var services = CreateServices(rewrapper.Object);

        var exitCode = await Parse(
                "secret-protection",
                "rewrap-recovery",
                "--owner",
                "tenant:acme",
                "--dry-run"
            )
            .RunAsync(services, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.NotNull(captured);
        Assert.True(captured.RecoveryOnly);
        Assert.True(captured.DryRun);
        Assert.Equal(SecretOwner.ForTenant("acme"), captured.Owner);
        Assert.True(captured.RecoveryPrivateKeyPem.IsEmpty);
    }

    [Fact]
    public async Task RunAsync_AFailedKey_ReturnsOne()
    {
        var rewrapper = CreateStrictMock<IMasterKeyRewrapper>();
        rewrapper
            .Setup(x =>
                x.RewrapAsync(It.IsAny<MasterKeyRewrapRequest>(), It.IsAny<CancellationToken>())
            )
            .Returns(new ValueTask<MasterKeyRewrapResult>(new MasterKeyRewrapResult(2, 1, 0, 1)));
        using var services = CreateServices(rewrapper.Object);

        var exitCode = await Parse("secret-protection", "rewrap")
            .RunAsync(services, CancellationToken.None);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task RunAsync_ReadsTheKeyFileIntoTheRequestAndZeroesItAfterwards()
    {
        const string pem = "-----BEGIN PRIVATE KEY-----\nAAAA\n-----END PRIVATE KEY-----\n";
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, pem, SecureEncoding.UTF8);
            string? seen = null;
            ReadOnlyMemory<char> memory = default;
            var rewrapper = CreateStrictMock<IMasterKeyRewrapper>();
            rewrapper
                .Setup(x =>
                    x.RewrapAsync(It.IsAny<MasterKeyRewrapRequest>(), It.IsAny<CancellationToken>())
                )
                .Callback<MasterKeyRewrapRequest, CancellationToken>(
                    (r, _) =>
                    {
                        memory = r.RecoveryPrivateKeyPem;
                        seen = memory.ToString();
                    }
                )
                .Returns(
                    new ValueTask<MasterKeyRewrapResult>(new MasterKeyRewrapResult(1, 1, 0, 0))
                );
            using var services = CreateServices(rewrapper.Object);

            var exitCode = await Parse(
                    "secret-protection",
                    "rewrap",
                    "--recovery-private-key",
                    path
                )
                .RunAsync(services, CancellationToken.None);

            Assert.Equal(0, exitCode);
            Assert.Equal(pem, seen);

            // The buffers were sensitive rentals, zeroed when the command ended.
            Assert.All(memory.ToArray(), c => Assert.Equal('\0', c));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RunAsync_AMissingKeyFile_ReturnsOneWithoutCallingTheRewrapper()
    {
        var rewrapper = CreateStrictMock<IMasterKeyRewrapper>();
        using var services = CreateServices(rewrapper.Object);

        var exitCode = await Parse(
                "secret-protection",
                "rewrap",
                "--recovery-private-key",
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
            )
            .RunAsync(services, CancellationToken.None);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task RunAsync_InvalidConfiguration_ReturnsOneWithoutCallingTheRewrapper()
    {
        var rewrapper = CreateStrictMock<IMasterKeyRewrapper>();
        var validator = CreateStrictMock<IStartupValidator>();
        validator
            .Setup(x => x.Validate())
            .Throws(new OptionsValidationException("x", typeof(object), ["bad"]));
        using var services = CreateServices(rewrapper.Object, validator.Object);

        var exitCode = await Parse("secret-protection", "rewrap")
            .RunAsync(services, CancellationToken.None);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task RunAsync_TheRewrapperThrows_ReturnsOne()
    {
        var rewrapper = CreateStrictMock<IMasterKeyRewrapper>();
        rewrapper
            .Setup(x =>
                x.RewrapAsync(It.IsAny<MasterKeyRewrapRequest>(), It.IsAny<CancellationToken>())
            )
            .Throws(new InvalidOperationException("boom"));
        using var services = CreateServices(rewrapper.Object);

        var exitCode = await Parse("secret-protection", "rewrap-recovery")
            .RunAsync(services, CancellationToken.None);

        Assert.Equal(1, exitCode);
    }

    #endregion
}
