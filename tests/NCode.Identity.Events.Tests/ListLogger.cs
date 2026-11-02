// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;

namespace NCode.Identity.Events;

/// <summary>
/// A test logger that captures the level, event id, formatted message and structured state of each
/// log entry so tests can assert which message was emitted and with which values.
/// </summary>
internal sealed class ListLogger<T> : ILogger<T>
{
    public List<(
        LogLevel Level,
        int EventId,
        string Message,
        IReadOnlyList<KeyValuePair<string, object?>>? State
    )> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    ) =>
        Entries.Add(
            (
                logLevel,
                eventId.Id,
                formatter(state, exception),
                state as IReadOnlyList<KeyValuePair<string, object?>>
            )
        );
}
