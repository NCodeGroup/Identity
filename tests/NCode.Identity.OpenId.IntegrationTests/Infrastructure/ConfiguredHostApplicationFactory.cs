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

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace NCode.Identity.OpenId.IntegrationTests.Infrastructure;

/// <summary>
/// A <see cref="HostApplicationFactory"/> whose configuration is overlaid with the supplied keys, the way an operator's
/// environment-specific settings or environment variables layer over the host's own <c>appsettings.json</c>.
/// </summary>
/// <param name="configuration">The configuration keys (colon-delimited paths) and values to overlay.</param>
public sealed class ConfiguredHostApplicationFactory(
    IReadOnlyDictionary<string, string?> configuration
) : HostApplicationFactory
{
    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration(configurationBuilder =>
            configurationBuilder.AddInMemoryCollection(configuration)
        );
    }
}
