// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;

namespace NCode.Identity.Events;

/// <summary>
/// A test logger factory that records the category name of each logger requested and hands every
/// caller the same <see cref="ListLogger{T}"/>, so tests can assert the category and the entries.
/// </summary>
internal sealed class ListLoggerFactory : ILoggerFactory
{
    public ListLogger<ListLoggerFactory> Logger { get; } = new();

    public List<string> CategoryNames { get; } = [];

    public ILogger CreateLogger(string categoryName)
    {
        CategoryNames.Add(categoryName);
        return Logger;
    }

    public void AddProvider(ILoggerProvider provider) { }

    public void Dispose() { }
}
