// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.ManagementConsole;

/// <summary>
/// Proves the composition of the management console host's <c>Program</c> entry point.
/// </summary>
public sealed class ManagementConsoleProgramTests
{
    #region Server Header Tests

    [Fact]
    public async Task Kestrel_WhenTheHostIsBuilt_SendsNoServerHeader()
    {
        await using var factory = new ManagementConsoleApplicationFactory();

        var options = factory.Services.GetRequiredService<IOptions<KestrelServerOptions>>().Value;

        Assert.False(options.AddServerHeader);
    }

    #endregion
}
