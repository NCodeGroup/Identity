// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using NCode.Identity.Bff.Logging;

namespace NCode.Identity.Bff.Sessions;

/// <summary>
/// The default <see cref="IBffConnectionRevalidator"/>: it validates the session of a connection at every revalidation
/// interval without sliding it, so the idle timeout still ends the session of an idle connection.
/// </summary>
/// <param name="timeProvider">The provider of the revalidation delay.</param>
/// <param name="logger">The logger.</param>
internal sealed class DefaultBffConnectionRevalidator(
    TimeProvider timeProvider,
    ILogger<DefaultBffConnectionRevalidator> logger
) : IBffConnectionRevalidator
{
    private TimeProvider TimeProvider { get; } = timeProvider;
    private ILogger<DefaultBffConnectionRevalidator> Logger { get; } = logger;

    /// <inheritdoc />
    public async ValueTask WaitUntilEndedAsync(
        IBffConnectionSession connection,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(connection);

        while (true)
        {
            await Task.Delay(connection.RevalidationInterval, TimeProvider, cancellationToken);

            try
            {
                if (await connection.ValidateAsync(null, cancellationToken) is null)
                    return;
            }
            catch (BffStoreUnavailableException exception)
            {
                Logger.ConnectionRevalidationFailed(exception);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                Logger.ConnectionRevalidationFaulted(exception);
                return;
            }
        }
    }
}
