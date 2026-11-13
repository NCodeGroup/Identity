// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Identity.OpenId.Management;
using NCode.Identity.OpenId.Setup;
using NCode.Identity.OpenId.Setup.Contracts;
using NCode.Identity.Server.InstanceSetup;
using Xunit;

namespace NCode.Identity.Server.Tests;

public class InstanceSetupCommandLineTests : BaseTests
{
    private const string UserName = "root";
    private const string Email = "root@example.com";
    private const string Password = "a-very-secret-password-1234";

    private static InstanceSetupCommandLine Parse(params string[] args)
    {
        var command = InstanceSetupCommandLine.Parse(args);
        Assert.NotNull(command);
        Assert.Null(command.Error);
        return command;
    }

    private static ServiceProvider CreateServices(
        IInstanceSetupService setupService,
        SetupRootOptions? root = null,
        CapturingLoggerProvider? loggerProvider = null,
        SetupManagementConsoleOptions? managementConsole = null,
        SetupAutomationOptions? automation = null
    )
    {
        var options = new SetupOptions { Root = root ?? new SetupRootOptions() };
        if (managementConsole is not null)
            options.ManagementConsole = managementConsole;
        if (automation is not null)
            options.Automation = automation;

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging(builder =>
        {
            if (loggerProvider is not null)
                builder.AddProvider(loggerProvider);
        });
        serviceCollection.AddSingleton(setupService);
        serviceCollection.AddSingleton(Options.Create(options));

        return serviceCollection.BuildServiceProvider();
    }

    private static SetupRootOptions CreateRoot() =>
        new()
        {
            UserName = UserName,
            Email = Email,
            Password = Password,
        };

    #region Parse Tests

    [Theory]
    [InlineData]
    [InlineData("--urls", "http://localhost")]
    [InlineData("secret-protection", "rewrap")]
    [InlineData("--setup")]
    public void Parse_ArgsThatAreNotTheCommand_ReturnNull(params string[] args)
    {
        Assert.Null(InstanceSetupCommandLine.Parse(args));
    }

    [Fact]
    public void Parse_TheCommandAlone_ReturnsACommandWithoutAnError()
    {
        var command = Parse("setup");

        Assert.Null(command.Error);
    }

    [Theory]
    [InlineData("setup", "--force")]
    [InlineData("setup", "extra")]
    [InlineData("setup", "--dry-run", "extra")]
    public void Parse_ExtraArguments_ReturnACommandWithAnError(params string[] args)
    {
        var command = InstanceSetupCommandLine.Parse(args);

        Assert.NotNull(command);
        Assert.False(string.IsNullOrWhiteSpace(command.Error));
    }

    #endregion

    #region GetHostArguments Tests

    [Fact]
    public void GetHostArguments_TheCommand_ReturnsNoArguments()
    {
        Assert.Empty(InstanceSetupCommandLine.GetHostArguments(["setup", "extra"]));
    }

    [Fact]
    public void GetHostArguments_OtherArguments_ReturnsThem()
    {
        string[] args = ["--urls", "http://localhost"];

        Assert.Equal(args, InstanceSetupCommandLine.GetHostArguments(args));
    }

    [Fact]
    public void GetHostArguments_NoArguments_ReturnsNone()
    {
        Assert.Empty(InstanceSetupCommandLine.GetHostArguments([]));
    }

    #endregion

    #region Usage Tests

    [Fact]
    public void Usage_NamesTheCommandAndItsConfiguration()
    {
        Assert.Contains("setup", InstanceSetupCommandLine.Usage, StringComparison.Ordinal);
        Assert.Contains(
            "Setup:Root:UserName",
            InstanceSetupCommandLine.Usage,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "Setup:Root:Password",
            InstanceSetupCommandLine.Usage,
            StringComparison.Ordinal
        );
    }

    #endregion

    #region RunAsync Tests

    [Fact]
    public async Task RunAsync_InvalidArguments_ReturnsTwoWithoutTouchingTheInstance()
    {
        var setupService = CreateStrictMock<IInstanceSetupService>();
        using var services = CreateServices(setupService.Object, CreateRoot());
        var command = InstanceSetupCommandLine.Parse(["setup", "extra"]);
        Assert.NotNull(command);

        var exitCode = await command.RunAsync(services, CancellationToken.None);

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task RunAsync_AlreadyInitialized_ReturnsZeroAndCreatesNothing()
    {
        var setupService = CreateStrictMock<IInstanceSetupService>();
        setupService
            .Setup(x => x.IsInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<bool>(true))
            .Verifiable();
        using var services = CreateServices(setupService.Object, CreateRoot());

        var exitCode = await Parse("setup").RunAsync(services, CancellationToken.None);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_AlreadyInitializedWithoutRootConfiguration_ReturnsZero()
    {
        var setupService = CreateStrictMock<IInstanceSetupService>();
        setupService
            .Setup(x => x.IsInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<bool>(true))
            .Verifiable();
        using var services = CreateServices(setupService.Object);

        var exitCode = await Parse("setup").RunAsync(services, CancellationToken.None);

        Assert.Equal(0, exitCode);
    }

    [Theory]
    [InlineData(null, Password, true, false)]
    [InlineData("", Password, true, false)]
    [InlineData("  ", Password, true, false)]
    [InlineData(UserName, null, false, true)]
    [InlineData(UserName, "", false, true)]
    [InlineData(null, null, true, true)]
    public async Task RunAsync_UserNameOrPasswordMissing_ReturnsTwoAndLogsEachMissingKey(
        string? userName,
        string? password,
        bool logsUserName,
        bool logsPassword
    )
    {
        var loggerProvider = new CapturingLoggerProvider();
        var setupService = CreateStrictMock<IInstanceSetupService>();
        setupService
            .Setup(x => x.IsInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<bool>(false))
            .Verifiable();
        var root = new SetupRootOptions
        {
            UserName = userName,
            Email = Email,
            Password = password,
        };
        using var services = CreateServices(setupService.Object, root, loggerProvider);

        var exitCode = await Parse("setup").RunAsync(services, CancellationToken.None);

        Assert.Equal(2, exitCode);
        Assert.Equal(
            logsUserName,
            loggerProvider.Messages.Any(message =>
                message.Contains("Setup:Root:UserName", StringComparison.Ordinal)
            )
        );
        Assert.Equal(
            logsPassword,
            loggerProvider.Messages.Any(message =>
                message.Contains("Setup:Root:Password", StringComparison.Ordinal)
            )
        );
        Assert.DoesNotContain(
            loggerProvider.Messages,
            message =>
                password is { Length: > 0 } && message.Contains(password, StringComparison.Ordinal)
        );
    }

    [Fact]
    public async Task RunAsync_EmailMissing_CreatesTheRoot()
    {
        InstanceSetupRequest? captured = null;
        var setupService = CreateStrictMock<IInstanceSetupService>();
        setupService
            .Setup(x => x.IsInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<bool>(false))
            .Verifiable();
        setupService
            .Setup(x =>
                x.SetUpAsync(It.IsAny<InstanceSetupRequest>(), It.IsAny<CancellationToken>())
            )
            .Callback<InstanceSetupRequest, CancellationToken>((r, _) => captured = r)
            .Returns(new ValueTask<InstanceSetupResult>(CreateSuccess()))
            .Verifiable();
        var root = new SetupRootOptions { UserName = UserName, Password = Password };
        using var services = CreateServices(setupService.Object, root);

        var exitCode = await Parse("setup").RunAsync(services, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.NotNull(captured);
        Assert.Null(captured.Root.Email);
    }

    [Fact]
    public async Task RunAsync_CreateRootFails_ReturnsOneAndLogsOnlyTheStatusCode()
    {
        var loggerProvider = new CapturingLoggerProvider();
        var setupService = CreateStrictMock<IInstanceSetupService>();
        setupService
            .Setup(x => x.IsInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<bool>(false))
            .Verifiable();
        setupService
            .Setup(x =>
                x.SetUpAsync(It.IsAny<InstanceSetupRequest>(), It.IsAny<CancellationToken>())
            )
            .Returns(
                new ValueTask<InstanceSetupResult>(
                    new InstanceSetupResult
                    {
                        Error = new ManagementError
                        {
                            StatusCode = 400,
                            Detail = "The password does not satisfy the password policy.",
                        },
                    }
                )
            )
            .Verifiable();
        using var services = CreateServices(setupService.Object, CreateRoot(), loggerProvider);

        var exitCode = await Parse("setup").RunAsync(services, CancellationToken.None);

        Assert.Equal(1, exitCode);
        Assert.Contains(
            loggerProvider.Messages,
            message => message.Contains("status code 400", StringComparison.Ordinal)
        );
        Assert.DoesNotContain(
            loggerProvider.Messages,
            message =>
                message.Contains(
                    "The password does not satisfy the password policy.",
                    StringComparison.Ordinal
                )
        );
    }

    [Fact]
    public async Task RunAsync_RootCreated_ReturnsZeroAndPassesTheConfiguredRoot()
    {
        InstanceSetupRequest? captured = null;
        var setupService = CreateStrictMock<IInstanceSetupService>();
        setupService
            .Setup(x => x.IsInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<bool>(false))
            .Verifiable();
        setupService
            .Setup(x =>
                x.SetUpAsync(It.IsAny<InstanceSetupRequest>(), It.IsAny<CancellationToken>())
            )
            .Callback<InstanceSetupRequest, CancellationToken>((r, _) => captured = r)
            .Returns(new ValueTask<InstanceSetupResult>(CreateSuccess()))
            .Verifiable();
        using var services = CreateServices(setupService.Object, CreateRoot());

        var exitCode = await Parse("setup").RunAsync(services, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.NotNull(captured);
        Assert.Equal(UserName, captured.Root.UserName);
        Assert.Equal(Email, captured.Root.Email);
        Assert.Equal(Password, captured.Root.Password);
        Assert.Null(captured.ManagementConsole);
    }

    [Fact]
    public async Task RunAsync_ManagementConsoleConfigured_PassesIt()
    {
        var loggerProvider = new CapturingLoggerProvider();
        InstanceSetupRequest? captured = null;
        var console = new SetupManagementConsoleOptions
        {
            ClientId = "mgmt-console",
            BaseUrl = "https://manage.example.com",
            Jwks = """{"keys":[{"kty":"EC"}]}""",
        };
        var setupService = CreateStrictMock<IInstanceSetupService>();
        setupService
            .Setup(x => x.IsInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<bool>(false))
            .Verifiable();
        setupService
            .Setup(x =>
                x.SetUpAsync(It.IsAny<InstanceSetupRequest>(), It.IsAny<CancellationToken>())
            )
            .Callback<InstanceSetupRequest, CancellationToken>((r, _) => captured = r)
            .Returns(
                new ValueTask<InstanceSetupResult>(
                    new InstanceSetupResult
                    {
                        Root = CreateSuccess().Root,
                        ManagementConsoleClientId = "mgmt-console",
                    }
                )
            )
            .Verifiable();
        using var services = CreateServices(
            setupService.Object,
            CreateRoot(),
            loggerProvider,
            console
        );

        var exitCode = await Parse("setup").RunAsync(services, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.NotNull(captured);
        Assert.Same(console, captured.ManagementConsole);
        Assert.Contains(
            loggerProvider.Messages,
            message => message.Contains("'mgmt-console'", StringComparison.Ordinal)
        );
    }

    [Fact]
    public async Task RunAsync_AutomationConfigured_PassesIt()
    {
        var loggerProvider = new CapturingLoggerProvider();
        InstanceSetupRequest? captured = null;
        var automation = new SetupAutomationOptions
        {
            ClientId = "platform-automation",
            Jwks = """{"keys":[{"kty":"EC"}]}""",
        };
        var setupService = CreateStrictMock<IInstanceSetupService>();
        setupService
            .Setup(x => x.IsInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<bool>(false))
            .Verifiable();
        setupService
            .Setup(x =>
                x.SetUpAsync(It.IsAny<InstanceSetupRequest>(), It.IsAny<CancellationToken>())
            )
            .Callback<InstanceSetupRequest, CancellationToken>((r, _) => captured = r)
            .Returns(
                new ValueTask<InstanceSetupResult>(
                    new InstanceSetupResult
                    {
                        Root = CreateSuccess().Root,
                        AutomationClientId = "platform-automation",
                    }
                )
            )
            .Verifiable();
        using var services = CreateServices(
            setupService.Object,
            CreateRoot(),
            loggerProvider,
            automation: automation
        );

        var exitCode = await Parse("setup").RunAsync(services, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.NotNull(captured);
        Assert.Same(automation, captured.Automation);
        Assert.Contains(
            loggerProvider.Messages,
            message => message.Contains("'platform-automation'", StringComparison.Ordinal)
        );
    }

    [Fact]
    public async Task RunAsync_AutomationUnset_PassesNone()
    {
        InstanceSetupRequest? captured = null;
        var setupService = CreateStrictMock<IInstanceSetupService>();
        setupService
            .Setup(x => x.IsInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<bool>(false))
            .Verifiable();
        setupService
            .Setup(x =>
                x.SetUpAsync(It.IsAny<InstanceSetupRequest>(), It.IsAny<CancellationToken>())
            )
            .Callback<InstanceSetupRequest, CancellationToken>((r, _) => captured = r)
            .Returns(new ValueTask<InstanceSetupResult>(CreateSuccess()))
            .Verifiable();
        using var services = CreateServices(setupService.Object, CreateRoot());

        var exitCode = await Parse("setup").RunAsync(services, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.NotNull(captured);
        Assert.Null(captured.Automation);
    }

    [Fact]
    public async Task RunAsync_AutomationRefused_Returns1()
    {
        var loggerProvider = new CapturingLoggerProvider();
        var automation = new SetupAutomationOptions { ClientId = "platform-automation" };
        var setupService = CreateStrictMock<IInstanceSetupService>();
        setupService
            .Setup(x => x.IsInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<bool>(false))
            .Verifiable();
        setupService
            .Setup(x =>
                x.SetUpAsync(It.IsAny<InstanceSetupRequest>(), It.IsAny<CancellationToken>())
            )
            .Returns(
                new ValueTask<InstanceSetupResult>(
                    new InstanceSetupResult
                    {
                        Error = new ManagementError
                        {
                            StatusCode = 400,
                            Detail = "The automation client needs a JWK Set.",
                        },
                    }
                )
            )
            .Verifiable();
        using var services = CreateServices(
            setupService.Object,
            CreateRoot(),
            loggerProvider,
            automation: automation
        );

        var exitCode = await Parse("setup").RunAsync(services, CancellationToken.None);

        Assert.Equal(1, exitCode);
        Assert.Contains(
            loggerProvider.Messages,
            message => message.Contains("status code 400", StringComparison.Ordinal)
        );
        Assert.DoesNotContain(
            loggerProvider.Messages,
            message =>
                message.Contains("The automation client needs a JWK Set.", StringComparison.Ordinal)
        );
    }

    [Fact]
    public async Task RunAsync_AnyOutcome_NeverLogsThePassword()
    {
        var loggerProvider = new CapturingLoggerProvider();
        var setupService = CreateStrictMock<IInstanceSetupService>();
        setupService
            .Setup(x => x.IsInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<bool>(false))
            .Verifiable();
        setupService
            .Setup(x =>
                x.SetUpAsync(It.IsAny<InstanceSetupRequest>(), It.IsAny<CancellationToken>())
            )
            .Returns(new ValueTask<InstanceSetupResult>(CreateSuccess()))
            .Verifiable();
        using var services = CreateServices(setupService.Object, CreateRoot(), loggerProvider);

        await Parse("setup").RunAsync(services, CancellationToken.None);

        Assert.NotEmpty(loggerProvider.Messages);
        Assert.DoesNotContain(
            loggerProvider.Messages,
            message => message.Contains(Password, StringComparison.Ordinal)
        );
    }

    #endregion

    private static InstanceSetupResult CreateSuccess() =>
        new()
        {
            Root = new CreateRootResponse
            {
                TenantId = "control-plane",
                PrincipalId = "principal-1",
                UserName = UserName,
            },
        };

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<string> Messages { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Messages);

        public void Dispose() { }
    }

    private sealed class CapturingLogger(List<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        ) => messages.Add(formatter(state, exception));
    }
}
