#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

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
