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

using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Logging.EventSource;
using Microsoft.Extensions.Options;
using NCode.Identity.Dispatch.Options;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests;

// The reference host logs only to standard output and to a diagnostics tool that attaches (ADR-0210).
/// <summary>
/// Provides unit tests for the <c>Program</c> entry point of the host project.
/// </summary>
public class ProgramTests
{
    #region Logging Tests

    [Fact]
    public async Task Logging_WhenTheHostIsBuilt_RegistersOnlyTheConsoleAndEventSourceProviders()
    {
        await using var factory = new HostApplicationFactory();

        var providers = factory
            .Services.GetServices<ILoggerProvider>()
            .Select(provider => provider.GetType())
            .OrderBy(type => type.Name, StringComparer.Ordinal);

        Assert.Equal([typeof(ConsoleLoggerProvider), typeof(EventSourceLoggerProvider)], providers);
    }

    [Fact]
    public async Task Logging_WhenTheJsonFormatterIsSelected_WritesEachEntryOnOneLine()
    {
        // The values the host image sets through its Logging__Console__ environment variables (ADR-0248).
        await using var factory = new ConfiguredHostApplicationFactory(
            new Dictionary<string, string?>
            {
                ["Logging:Console:FormatterName"] = "json",
                ["Logging:Console:FormatterOptions:UseUtcTimestamp"] = "true",
                ["Logging:Console:FormatterOptions:TimestampFormat"] = "O",
            }
        );

        var optionsMonitor = factory.Services.GetRequiredService<
            IOptionsMonitor<ConsoleLoggerOptions>
        >();
        var formatterName = optionsMonitor.CurrentValue.FormatterName;
        Assert.Equal("json", formatterName);

        var formatter = factory
            .Services.GetServices<ConsoleFormatter>()
            .Single(candidate => candidate.Name == formatterName);

        var exception = CreateException();
        var entry = new LogEntry<string>(
            LogLevel.Error,
            "Test",
            new EventId(1),
            "message",
            exception,
            static (state, _) => state
        );
        using var writer = new StringWriter();
        formatter.Write(in entry, null, writer);
        var text = writer.ToString().TrimEnd('\r', '\n');

        Assert.DoesNotContain("\n", text);
        var jsonElement = JsonElement.Parse(text);
        var exceptionText = jsonElement.GetProperty("Exception").GetString();
        var timestamp = jsonElement.GetProperty("Timestamp").GetString();
        Assert.Contains("second line", exceptionText);
        Assert.EndsWith("+00:00", timestamp);
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public async Task Configuration_WhenTheInProcessJobDispatcherIsConfigured_BindsIt()
    {
        await using var factory = new ConfiguredHostApplicationFactory(
            new Dictionary<string, string?>
            {
                ["OpenId:InProcessJobDispatcher:MaxConcurrentJobs"] = "4",
                ["OpenId:InProcessJobDispatcher:FullMode"] = "DropOldest",
            }
        );

        var options = factory
            .Services.GetRequiredService<IOptions<InProcessJobDispatcherOptions>>()
            .Value;

        Assert.Equal(4, options.MaxConcurrentJobs);
        Assert.Equal(BoundedChannelFullMode.DropOldest, options.FullMode);
    }

    #endregion

    // An exception that was thrown, so it has a stack trace, with a message that spans two lines.
    private static InvalidOperationException CreateException()
    {
        try
        {
            throw new InvalidOperationException("first line\nsecond line");
        }
        catch (InvalidOperationException exception)
        {
            return exception;
        }
    }
}
