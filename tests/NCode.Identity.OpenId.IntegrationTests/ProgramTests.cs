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

using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
}
