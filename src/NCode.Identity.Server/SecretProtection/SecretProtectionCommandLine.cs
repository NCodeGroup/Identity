// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Buffers;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Server.Logging;

namespace NCode.Identity.Server.SecretProtection;

/// <summary>
/// The offline <c>secret-protection</c> command of the host: <c>rewrap</c> rewraps every master key under the
/// configured root key provider and recovery public key, and <c>rewrap-recovery</c> replaces only the recovery copies.
/// The host runs it once, for example as a Kubernetes Job from its own image, and exits without starting the server.
/// </summary>
/// <remarks>
/// <para>
/// A master key is unwrapped by the root key provider it names, or from its recovery copy when
/// <c>--recovery-private-key</c> names the file of the recovery private key. That key is read from the file, never from
/// configuration, into sensitive buffers that are zeroed when the command ends, and it exists only inside this command.
/// </para>
/// <para>
/// The exit code is <c>0</c> when no master key failed, <c>1</c> otherwise or when the configuration is invalid, and
/// <c>2</c> for a usage error.
/// </para>
/// </remarks>
// See ADR-1408.
[PublicAPI]
public sealed class SecretProtectionCommandLine
{
    /// <summary>
    /// The first argument that selects this command.
    /// </summary>
    public const string CommandName = "secret-protection";

    /// <summary>
    /// The verb that rewraps every master key under the configured root key provider and recovery public key.
    /// </summary>
    public const string RewrapVerb = "rewrap";

    /// <summary>
    /// The verb that replaces only the recovery copies of the master keys.
    /// </summary>
    public const string RewrapRecoveryVerb = "rewrap-recovery";

    /// <summary>
    /// The option that names the file of the recovery private key.
    /// </summary>
    public const string RecoveryPrivateKeyOption = "--recovery-private-key";

    /// <summary>
    /// The option that limits the command to the master keys of one owner.
    /// </summary>
    public const string OwnerOption = "--owner";

    /// <summary>
    /// The option that verifies the master keys without writing anything.
    /// </summary>
    public const string DryRunOption = "--dry-run";

    // A PEM file is a few kilobytes; a file larger than this is not a recovery key.
    private const int MaxKeyFileSizeBytes = 64 * 1024;

    private SecretProtectionCommandLine() { }

    /// <summary>
    /// Gets the usage text of the command.
    /// </summary>
    public static string Usage { get; } =
        $"Usage: {CommandName} {RewrapVerb}|{RewrapRecoveryVerb} "
        + $"[{RecoveryPrivateKeyOption} <file>] [{OwnerOption} <type:id>] [{DryRunOption}]";

    /// <summary>
    /// Gets the verb: <see cref="RewrapVerb"/> or <see cref="RewrapRecoveryVerb"/>.
    /// </summary>
    public string Verb { get; private init; } = RewrapVerb;

    /// <summary>
    /// Gets the path of the file that holds the recovery private key, or <see langword="null"/> when each master key is
    /// unwrapped by the root key provider it names.
    /// </summary>
    public string? RecoveryPrivateKeyPath { get; private init; }

    /// <summary>
    /// Gets the owner whose master keys the command rewraps, or <see langword="null"/> for every owner.
    /// </summary>
    public SecretOwner? Owner { get; private init; }

    /// <summary>
    /// Gets a value indicating whether the command only verifies the master keys.
    /// </summary>
    public bool DryRun { get; private init; }

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
    public static SecretProtectionCommandLine? Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Count == 0 || !string.Equals(args[0], CommandName, StringComparison.Ordinal))
            return null;

        if (
            args.Count < 2
            || (
                !string.Equals(args[1], RewrapVerb, StringComparison.Ordinal)
                && !string.Equals(args[1], RewrapRecoveryVerb, StringComparison.Ordinal)
            )
        )
        {
            return new SecretProtectionCommandLine
            {
                Error =
                    $"The {CommandName} command takes the verb '{RewrapVerb}' or '{RewrapRecoveryVerb}'.",
            };
        }

        string? path = null;
        SecretOwner? owner = null;
        var dryRun = false;

        for (var index = 2; index < args.Count; index++)
        {
            var option = args[index];
            switch (option)
            {
                case RecoveryPrivateKeyOption:
                    if (path is not null)
                        return new SecretProtectionCommandLine { Error = RepeatedMessage(option) };

                    if (!TryReadValue(args, ref index, out path))
                        return new SecretProtectionCommandLine
                        {
                            Error = MissingValueMessage(option),
                        };

                    break;

                case OwnerOption:
                    if (owner is not null)
                        return new SecretProtectionCommandLine { Error = RepeatedMessage(option) };

                    if (!TryReadValue(args, ref index, out var ownerText))
                        return new SecretProtectionCommandLine
                        {
                            Error = MissingValueMessage(option),
                        };

                    if (!SecretOwner.TryParse(ownerText, out var parsed))
                    {
                        return new SecretProtectionCommandLine
                        {
                            Error =
                                $"The value of {OwnerOption} must be an owner in the form <type:id>, such as tenant:acme.",
                        };
                    }

                    owner = parsed;
                    break;

                case DryRunOption:
                    if (dryRun)
                        return new SecretProtectionCommandLine { Error = RepeatedMessage(option) };

                    dryRun = true;
                    break;

                default:
                    return new SecretProtectionCommandLine
                    {
                        Error = $"The option '{option}' is not recognized.",
                    };
            }
        }

        return new SecretProtectionCommandLine
        {
            Verb = args[1],
            RecoveryPrivateKeyPath = path,
            Owner = owner,
            DryRun = dryRun,
        };
    }

    /// <summary>
    /// Gets the arguments that the host builds with: none when they select this command, because a command-line
    /// configuration provider would read the verb and its options as configuration, and the arguments otherwise.
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
            .CreateLogger<SecretProtectionCommandLine>();

        if (Error is { } error)
        {
            logger.SecretProtectionCommandUsageInvalid(error, Usage);
            return 2;
        }

        // The command checks the same configuration the host checks when it starts.
        try
        {
            services.GetService<IStartupValidator>()?.Validate();
        }
        catch (Exception exception)
            when (exception is OptionsValidationException
                || (
                    exception is AggregateException aggregate
                    && aggregate.InnerExceptions.All(inner => inner is OptionsValidationException)
                )
            )
        {
            // The startup validator throws one exception per options type, or an aggregate when several fail.
            logger.SecretProtectionCommandConfigurationInvalid(exception);
            return 1;
        }

        var rewrapper = services.GetRequiredService<IMasterKeyRewrapper>();

        try
        {
            return await RewrapAsync(rewrapper, logger, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.SecretProtectionCommandFailed(exception);
            return 1;
        }
    }

    private async ValueTask<int> RewrapAsync(
        IMasterKeyRewrapper rewrapper,
        ILogger logger,
        CancellationToken cancellationToken
    )
    {
        // Both buffers are sensitive: they are zeroed when they are released, whether the rewrap succeeds or fails.
        using var lease = await ReadRecoveryPrivateKeyAsync(logger, cancellationToken);
        if (RecoveryPrivateKeyPath is not null && lease is null)
            return 1;

        var result = await rewrapper.RewrapAsync(
            new MasterKeyRewrapRequest
            {
                RecoveryOnly = string.Equals(Verb, RewrapRecoveryVerb, StringComparison.Ordinal),
                Owner = Owner,
                DryRun = DryRun,
                RecoveryPrivateKeyPem = lease?.Pem ?? default,
            },
            cancellationToken
        );

        if (Owner is { } owner && result.Examined == 0)
        {
            logger.SecretProtectionCommandOwnerHasNoMasterKeys(owner);
            return 1;
        }

        return result.Failed == 0 ? 0 : 1;
    }

    // Reads the recovery private key file into sensitive buffers, or returns null when no file is named or it cannot
    // be read, which is logged.
    private async ValueTask<RecoveryKeyLease?> ReadRecoveryPrivateKeyAsync(
        ILogger logger,
        CancellationToken cancellationToken
    )
    {
        if (RecoveryPrivateKeyPath is not { } path)
            return null;

        // A cancelled command opens no key file and rents no sensitive buffer.
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 1,
                FileOptions.Asynchronous
            );

            var length = stream.Length;
            if (length is <= 0 or > MaxKeyFileSizeBytes)
            {
                logger.SecretProtectionRecoveryKeyUnreadable(path, null);
                return null;
            }

            var size = (int)length;
            var bytesLease = BufferFactory.Rent(size, isSensitive: true, out Memory<byte> bytes);
            var charsLease = BufferFactory.Rent<char>(
                size,
                isSensitive: true,
                out Memory<char> chars
            );
            var lease = new RecoveryKeyLease(bytesLease, charsLease);
            try
            {
                await stream.ReadExactlyAsync(bytes[..size], cancellationToken);
                var written = SecureEncoding.ASCII.GetChars(bytes.Span[..size], chars.Span[..size]);
                lease.Pem = chars[..written];
                return lease;
            }
            catch
            {
                lease.Dispose();
                throw;
            }
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            logger.SecretProtectionRecoveryKeyUnreadable(path, exception);
            return null;
        }
    }

    private static bool TryReadValue(IReadOnlyList<string> args, ref int index, out string value)
    {
        if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            value = string.Empty;
            return false;
        }

        value = args[++index];
        return true;
    }

    private static string RepeatedMessage(string option) =>
        $"The option '{option}' can be given only once.";

    private static string MissingValueMessage(string option) =>
        $"The option '{option}' needs a value.";
}
