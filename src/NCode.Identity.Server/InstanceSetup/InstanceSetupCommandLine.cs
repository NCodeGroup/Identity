// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Setup;
using NCode.Identity.Server.Logging;

namespace NCode.Identity.Server.InstanceSetup;

/// <summary>
/// The headless <c>setup</c> command of the host: it creates the root administrator of an uninitialized instance from
/// the <c>Setup:Root:UserName</c>, <c>Setup:Root:Email</c> and <c>Setup:Root:Password</c> configuration through the same
/// <see cref="IInstanceSetupService"/> that the setup wizard uses, and exits without starting the server. The host runs
/// it once, for example as a Kubernetes Job from its own image, so it needs no setup token: running the image with the
/// database connection is already full authority over the instance.
/// </summary>
/// <remarks>
/// The exit code is <c>0</c> when the root administrator is created or the instance is already initialized, so a Job can
/// run the command on every release, <c>1</c> when the root administrator cannot be created, and <c>2</c> for a usage
/// error or when the configuration lacks the root administrator's username or password.
/// </remarks>
// See ADR-1512.
[PublicAPI]
public sealed class InstanceSetupCommandLine
{
    /// <summary>
    /// The first argument that selects this command.
    /// </summary>
    public const string CommandName = "setup";

    private const string UserNameKey = $"{SetupOptions.SectionName}:Root:UserName";
    private const string PasswordKey = $"{SetupOptions.SectionName}:Root:Password";

    private InstanceSetupCommandLine() { }

    /// <summary>
    /// Gets the usage text of the command.
    /// </summary>
    public static string Usage { get; } =
        $"Usage: {CommandName} (the root administrator comes from the {UserNameKey}, "
        + $"{SetupOptions.SectionName}:Root:Email and {PasswordKey} configuration; the optional "
        + $"{SetupOptions.SectionName}:ManagementConsole and {SetupOptions.SectionName}:Automation "
        + "sections register the management console's client and an automation client)";

    /// <summary>
    /// Gets the reason the arguments are invalid, or <see langword="null"/> when they are. A command with an error
    /// logs it with the <see cref="Usage"/> when it runs and exits with <c>2</c>.
    /// </summary>
    public string? Error { get; private init; }

    /// <summary>
    /// Parses the command line of the host.
    /// </summary>
    /// <param name="args">The command-line arguments of the host.</param>
    /// <returns>The command, or <see langword="null"/> when the arguments do not start with <see cref="CommandName"/>.
    /// A command whose <see cref="Error"/> is set names what is wrong with the arguments.</returns>
    public static InstanceSetupCommandLine? Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Count == 0 || !string.Equals(args[0], CommandName, StringComparison.Ordinal))
            return null;

        return args.Count == 1
            ? new InstanceSetupCommandLine()
            : new InstanceSetupCommandLine
            {
                Error =
                    $"The {CommandName} command takes no arguments; '{args[1]}' is not recognized.",
            };
    }

    /// <summary>
    /// Gets the arguments that the host builds with: none when they select this command, because a command-line
    /// configuration provider would read its arguments as configuration, and the arguments otherwise.
    /// </summary>
    /// <param name="args">The command-line arguments of the host.</param>
    /// <returns>The arguments for the host builder.</returns>
    public static string[] GetHostArguments(string[] args)
    {
        return HostCommandLine.GetHostArguments(args, CommandName);
    }

    /// <summary>
    /// Runs the command against the services of a built host, without starting the host.
    /// </summary>
    /// <param name="services">The services of the built host.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the command.</param>
    /// <returns>The <see cref="ValueTask{TResult}"/> that represents the command, containing its exit code.</returns>
    public async ValueTask<int> RunAsync(
        IServiceProvider services,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(services);

        var logger = services
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger<InstanceSetupCommandLine>();

        if (Error is { } error)
        {
            logger.InstanceSetupCommandUsageInvalid(error, Usage);
            return 2;
        }

        var setupService = services.GetRequiredService<IInstanceSetupService>();
        if (await setupService.IsInitializedAsync(cancellationToken))
        {
            logger.InstanceSetupCommandAlreadyInitialized();
            return 0;
        }

        var options = services.GetRequiredService<IOptions<SetupOptions>>().Value;
        var root = options.Root;

        var isUserNameMissing = string.IsNullOrWhiteSpace(root.UserName);
        if (isUserNameMissing)
            logger.InstanceSetupCommandRootUserNameMissing();

        var isPasswordMissing = string.IsNullOrEmpty(root.Password);
        if (isPasswordMissing)
            logger.InstanceSetupCommandRootPasswordMissing();

        if (isUserNameMissing || isPasswordMissing)
            return 2;

        var result = await setupService.SetUpAsync(
            new InstanceSetupRequest
            {
                Root = root,
                ManagementConsole = options.ManagementConsole.IsConfigured
                    ? options.ManagementConsole
                    : null,
                Automation = options.Automation.IsConfigured ? options.Automation : null,
            },
            cancellationToken
        );
        if (!result.Succeeded)
        {
            logger.InstanceSetupCommandFailed(result.Error.StatusCode);
            return 1;
        }

        logger.InstanceSetupCommandRootCreated(result.Root.UserName, result.Root.TenantId);
        if (result.Root.ManagementConsoleClientId is { } consoleClientId)
            logger.InstanceSetupCommandManagementConsoleRegistered(consoleClientId);
        if (result.AutomationClientId is { } automationClientId)
            logger.InstanceSetupCommandAutomationClientCreated(automationClientId);

        return 0;
    }
}
