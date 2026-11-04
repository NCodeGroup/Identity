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
/// The exit code is <c>0</c> when no master key failed and <c>1</c> otherwise or when the configuration is invalid; the
/// host returns <c>2</c> for a usage error.
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
    /// Parses the command line of the host.
    /// </summary>
    /// <param name="args">The command-line arguments of the host.</param>
    /// <param name="commandLine">The parsed command, when the method returns <see langword="true"/>.</param>
    /// <param name="error">The reason the arguments are invalid, when the arguments select this command and are wrong.</param>
    /// <returns><see langword="true"/> when the arguments are this command. <see langword="false"/> with a
    /// <see langword="null"/> <paramref name="error"/> when the arguments do not start with <see cref="CommandName"/>,
    /// and <see langword="false"/> with an <paramref name="error"/> when they do but are invalid.</returns>
    public static bool TryParse(
        IReadOnlyList<string> args,
        out SecretProtectionCommandLine? commandLine,
        out string? error
    )
    {
        ArgumentNullException.ThrowIfNull(args);

        commandLine = null;
        error = null;

        if (args.Count == 0 || !string.Equals(args[0], CommandName, StringComparison.Ordinal))
            return false;

        if (
            args.Count < 2
            || (
                !string.Equals(args[1], RewrapVerb, StringComparison.Ordinal)
                && !string.Equals(args[1], RewrapRecoveryVerb, StringComparison.Ordinal)
            )
        )
        {
            error =
                $"The {CommandName} command takes the verb '{RewrapVerb}' or '{RewrapRecoveryVerb}'.";
            return false;
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
                        return Fail(RepeatedMessage(option), out error);

                    if (!TryReadValue(args, ref index, out path))
                        return Fail(MissingValueMessage(option), out error);

                    break;

                case OwnerOption:
                    if (owner is not null)
                        return Fail(RepeatedMessage(option), out error);

                    if (!TryReadValue(args, ref index, out var ownerText))
                        return Fail(MissingValueMessage(option), out error);

                    if (!SecretOwner.TryParse(ownerText, out var parsed))
                    {
                        return Fail(
                            $"The value of {OwnerOption} must be an owner in the form <type:id>, such as tenant:acme.",
                            out error
                        );
                    }

                    owner = parsed;
                    break;

                case DryRunOption:
                    if (dryRun)
                        return Fail(RepeatedMessage(option), out error);

                    dryRun = true;
                    break;

                default:
                    return Fail($"The option '{option}' is not recognized.", out error);
            }
        }

        commandLine = new SecretProtectionCommandLine
        {
            Verb = args[1],
            RecoveryPrivateKeyPath = path,
            Owner = owner,
            DryRun = dryRun,
        };
        return true;
    }

    /// <summary>
    /// Runs the command against the services of a built host, without starting the host.
    /// </summary>
    /// <param name="services">The services of the built host.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the command.</param>
    /// <returns>The <see cref="Task"/> that represents the command, containing its exit code.</returns>
    public async Task<int> RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        var logger = services
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger<SecretProtectionCommandLine>();

        // The command checks the same configuration the host checks when it starts.
        try
        {
            services.GetService<IStartupValidator>()?.Validate();
        }
        catch (OptionsValidationException exception)
        {
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

    private async Task<int> RewrapAsync(
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
    private async Task<RecoveryKeyLease?> ReadRecoveryPrivateKeyAsync(
        ILogger logger,
        CancellationToken cancellationToken
    )
    {
        if (RecoveryPrivateKeyPath is not { } path)
            return null;

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

    private static bool Fail(string message, out string? error)
    {
        error = message;
        return false;
    }

    private static string RepeatedMessage(string option) =>
        $"The option '{option}' can be given only once.";

    private static string MissingValueMessage(string option) =>
        $"The option '{option}' needs a value.";

    // Holds the two sensitive buffers of the recovery private key until the command ends.
    private sealed class RecoveryKeyLease(IDisposable bytesLease, IDisposable charsLease)
        : IDisposable
    {
        public ReadOnlyMemory<char> Pem { get; set; }

        public void Dispose()
        {
            bytesLease.Dispose();
            charsLease.Dispose();
        }
    }
}
